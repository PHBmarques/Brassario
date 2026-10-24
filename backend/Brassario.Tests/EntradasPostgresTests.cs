using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-18: a entrada composta é tudo ou nada de verdade, contra PostgreSQL real.</summary>
    [Trait("Categoria", "Docker")]
    public class EntradasPostgresTests : IClassFixture<ConcorrenciaPostgresFixture>
    {
        private static readonly DateOnly Hoje = new(2026, 10, 19);
        private readonly ConcorrenciaPostgresFixture _banco;

        public EntradasPostgresTests(ConcorrenciaPostgresFixture banco) => _banco = banco;

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => EntradasPostgresTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        }

        private static EntradaService Montar(AppDbContext db)
        {
            var relogio = new RelogioFixo();
            var insumos = new InsumoRepository(db);
            var fornecedores = new FornecedorRepository(db);
            var lotes = new LoteRepository(db);

            return new EntradaService(
                new LoteService(lotes, insumos, fornecedores, relogio),
                new InsumoService(insumos, lotes, relogio),
                new FornecedorService(fornecedores),
                lotes, relogio);
        }

        private async Task<long> PrepararAsync()
        {
            await using var db = _banco.NovoContexto();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE usuario, fornecedor, insumo RESTART IDENTITY CASCADE");

            var usuario = new Usuario { Nome = "Operador", Email = "op@teste.local", SenhaHash = "x", Perfil = PerfilUsuario.OPERADOR };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            return usuario.Id;
        }

        private static EntradaLoteRequest Lote(string? codigo = "L-001", decimal quantidade = 25) => new()
        {
            CodigoLote = codigo,
            DataValidade = new DateOnly(2027, 3, 1),
            Quantidade = quantidade,
            PrecoUnitario = 8.5m
        };

        private static InsumoRequest InsumoNovo(string nome = "Lúpulo Cascade") => new()
        {
            Nome = nome, Categoria = "LUPULO", UnidadeMedida = "G", EstoqueMinimo = 500, DiasValidadeCritica = 30, DiasValidadeAtencao = 90
        };

        private async Task<(int Insumos, int Fornecedores, int Lotes, int Movimentacoes)> ContarAsync()
        {
            await using var db = _banco.NovoContexto();
            return (await db.Insumos.CountAsync(), await db.Fornecedores.CountAsync(),
                    await db.LotesInsumo.CountAsync(), await db.Movimentacoes.CountAsync());
        }

        [Fact]
        public async Task Insumo_novo_e_fornecedor_novo_gravam_insumo_fornecedor_lote_e_movimentacao_juntos()
        {
            var usuario = await PrepararAsync();

            await using (var db = _banco.NovoContexto())
            {
                var r = await Montar(db).RegistrarAsync(new EntradaRequest
                {
                    Lote = Lote(quantidade: 800),
                    NovoInsumo = InsumoNovo(),
                    NovoFornecedor = new FornecedorRequest { Nome = "Hop Brasil" }
                }, usuario);

                Assert.True(r.Insumo.Id > 0);
                Assert.True(r.Fornecedor!.Id > 0);
                Assert.Equal(r.Insumo.Id, r.Lote.InsumoId);
                Assert.Equal(r.Fornecedor.Id, r.Lote.FornecedorId);
                Assert.Equal(800, r.SaldoAtual);
            }

            Assert.Equal((1, 1, 1, 1), await ContarAsync());

            await using var conferencia = _banco.NovoContexto();
            var mov = await conferencia.Movimentacoes.SingleAsync();
            Assert.Equal(TipoMovimentacao.ENTRADA, mov.Tipo);
            Assert.Equal(usuario, mov.UsuarioId);
            Assert.Equal(800, mov.Quantidade);
        }

        [Fact]
        public async Task Falha_do_banco_no_meio_da_gravacao_desfaz_insumo_e_fornecedor_novos()
        {
            var usuario = await PrepararAsync();

            // 1e12 passa pelas regras do serviço, mas estoura numeric(12,3) no INSERT do lote,
            // que é o último passo: o insumo e o fornecedor já tinham entrado na mesma gravação.
            await using (var db = _banco.NovoContexto())
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => Montar(db).RegistrarAsync(new EntradaRequest
                {
                    Lote = Lote(quantidade: 1_000_000_000_000m),
                    NovoInsumo = InsumoNovo(),
                    NovoFornecedor = new FornecedorRequest { Nome = "Hop Brasil" }
                }, usuario));
            }

            Assert.Equal((0, 0, 0, 0), await ContarAsync());
        }

        [Fact]
        public async Task Nome_de_insumo_repetido_devolve_NOME_DUPLICADO_e_nao_grava_o_fornecedor_novo()
        {
            var usuario = await PrepararAsync();
            await using (var db = _banco.NovoContexto())
                await Montar(db).RegistrarAsync(new EntradaRequest { Lote = Lote(), NovoInsumo = InsumoNovo("Malte Pilsen") }, usuario);

            await using (var db = _banco.NovoContexto())
            {
                var ex = await Assert.ThrowsAsync<ConflitoException>(() => Montar(db).RegistrarAsync(new EntradaRequest
                {
                    Lote = Lote("L-002"),
                    NovoInsumo = InsumoNovo("  MALTE pilsen "),
                    NovoFornecedor = new FornecedorRequest { Nome = "Hop Brasil" }
                }, usuario));

                Assert.Equal("NOME_DUPLICADO", ex.Codigo);
                Assert.Equal("novoInsumo.nome", Assert.Single(ex.Detalhes!).Key);
            }

            Assert.Equal((1, 0, 1, 1), await ContarAsync());   // só a primeira entrada
        }

        [Fact]
        public async Task Saldo_atual_na_resposta_soma_o_lote_novo_com_os_que_ja_existiam()
        {
            var usuario = await PrepararAsync();
            long insumoId;
            await using (var db = _banco.NovoContexto())
                insumoId = (await Montar(db).RegistrarAsync(new EntradaRequest { Lote = Lote("L-001", 100), NovoInsumo = InsumoNovo() }, usuario)).Insumo.Id;

            await using var db2 = _banco.NovoContexto();
            var r = await Montar(db2).RegistrarAsync(new EntradaRequest { Lote = Lote("L-002", 25), InsumoId = insumoId }, usuario);

            Assert.Equal(125, r.SaldoAtual);
            Assert.Null(r.Fornecedor);
        }

        [Fact]
        public async Task Codigo_gerado_para_insumo_novo_e_gravado_e_a_sequencia_continua_na_entrada_seguinte()
        {
            var usuario = await PrepararAsync();
            long insumoId;
            await using (var db = _banco.NovoContexto())
            {
                var r = await Montar(db).RegistrarAsync(new EntradaRequest { Lote = Lote(codigo: ""), NovoInsumo = InsumoNovo() }, usuario);
                Assert.Equal("L-20261019-01", r.Lote.CodigoLote);
                insumoId = r.Insumo.Id;
            }

            await using var db2 = _banco.NovoContexto();
            var segunda = await Montar(db2).RegistrarAsync(new EntradaRequest { Lote = Lote(codigo: null), InsumoId = insumoId }, usuario);

            Assert.Equal("L-20261019-02", segunda.Lote.CodigoLote);
        }
    }
}
