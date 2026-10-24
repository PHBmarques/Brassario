using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>
    /// API-20 no PostgreSQL de verdade: baixa FEFO gravada (lote, consumo, movimentação, Versao),
    /// LOTE_VENCIDO e saldo insuficiente sem efeito parcial, e a consulta dos consumos.
    /// A disputa de duas produções simultâneas pelo mesmo saldo já é provada em ConcorrenciaPostgresTests (TST-4).
    /// </summary>
    [Trait("Categoria", "Docker")]
    public class BaixaFefoPostgresTests : IClassFixture<ConcorrenciaPostgresFixture>
    {
        private readonly ConcorrenciaPostgresFixture _banco;

        public BaixaFefoPostgresTests(ConcorrenciaPostgresFixture banco) => _banco = banco;

        private class RelogioReal : IRelogio
        {
            public DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);
            public DateTime AgoraUtc => DateTime.UtcNow;
        }

        private class ReceitaDoBanco : IReceitaService
        {
            private readonly AppDbContext _db;
            public ReceitaDoBanco(AppDbContext db) => _db = db;

            public Task<Receita> ObterParaProducaoAsync(long id, CancellationToken ct = default) =>
                _db.Receitas.AsNoTracking().Include(r => r.Itens).ThenInclude(i => i.Insumo).SingleAsync(r => r.Id == id, ct);

            public Task<List<ReceitaResumoResponse>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> ObterAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> CriarAsync(ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AtualizarAsync(long id, ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AlterarStatusAsync(long id, bool ativa, CancellationToken ct = default) => throw new NotSupportedException();
        }

        private sealed record Cenario(long ReceitaId, long UsuarioId);

        // (código, validade em dias a partir de hoje, saldo): 18 kg de malte por 100 L
        private async Task<Cenario> PrepararAsync(params (string Codigo, int ValidadeEmDias, decimal Saldo)[] lotes)
        {
            await using var db = _banco.NovoContexto();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE usuario, fornecedor, insumo, receita RESTART IDENTITY CASCADE");
            var hoje = new RelogioReal().Hoje;

            var malte = new Insumo
            {
                Nome = "Malte Pilsen", Categoria = CategoriaInsumo.MALTE, UnidadeMedida = UnidadeMedida.KG,
                EstoqueMinimo = 0, DiasValidadeCritica = 15, DiasValidadeAtencao = 45, Ativo = true
            };
            var receita = new Receita { Nome = "IPA Cascade", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { Insumo = malte, Quantidade = 18m });
            db.Receitas.Add(receita);

            var usuario = new Usuario { Nome = "Operador", Email = "operador@teste.local", SenhaHash = "hash", Perfil = PerfilUsuario.OPERADOR };
            db.Usuarios.Add(usuario);

            foreach (var (codigo, dias, saldo) in lotes)
            {
                db.LotesInsumo.Add(new LoteInsumo
                {
                    Insumo = malte, CodigoLote = codigo,
                    DataEntrada = hoje.AddDays(-60), DataValidade = hoje.AddDays(dias),
                    QuantidadeInicial = saldo, QuantidadeAtual = saldo, PrecoUnitario = 8.5m
                });
            }

            await db.SaveChangesAsync();
            return new Cenario(receita.Id, usuario.Id);
        }

        private static BateladaService NovoServico(AppDbContext db) =>
            new(new ReceitaDoBanco(db), new BateladaRepository(db), new RelogioReal());

        [Fact]
        public async Task Baixa_FEFO_grava_lotes_consumos_movimentacoes_e_versao_e_nao_toca_no_vencido()
        {
            var c = await PrepararAsync(("M-VENCIDO", -1, 100m), ("M-TARDE", 90, 40m), ("M-CEDO", 30, 10m));

            BateladaResponse r;
            await using (var db = _banco.NovoContexto())
                r = await NovoServico(db).ProduzirAsync(new BateladaRequest { ReceitaId = c.ReceitaId, VolumePlanejado = 100m }, c.UsuarioId);

            Assert.Equal(new[] { ("M-CEDO", 10m), ("M-TARDE", 8m) }, r.Consumos.Select(x => (x.CodigoLote, x.Quantidade)));

            await using var leitura = _banco.NovoContexto();
            var lotes = await leitura.LotesInsumo.AsNoTracking().ToDictionaryAsync(l => l.CodigoLote);
            Assert.Equal(0m, lotes["M-CEDO"].QuantidadeAtual);
            Assert.Equal(32m, lotes["M-TARDE"].QuantidadeAtual);
            Assert.Equal(100m, lotes["M-VENCIDO"].QuantidadeAtual);
            Assert.Equal(1, lotes["M-CEDO"].Versao);
            Assert.Equal(1, lotes["M-TARDE"].Versao);
            Assert.Equal(0, lotes["M-VENCIDO"].Versao);

            var movs = await leitura.Movimentacoes.AsNoTracking().OrderBy(m => m.Id).ToListAsync();
            Assert.Equal(2, movs.Count);
            Assert.All(movs, m =>
            {
                Assert.Equal(TipoMovimentacao.SAIDA_PRODUCAO, m.Tipo);
                Assert.Equal(r.Id, m.BateladaId);
                Assert.Equal(c.UsuarioId, m.UsuarioId);
            });
            Assert.Equal(-18m, movs.Sum(m => m.Quantidade));
        }

        [Fact]
        public async Task Consulta_dos_consumos_devolve_os_lotes_usados_e_404_para_producao_inexistente()
        {
            var c = await PrepararAsync(("M-TARDE", 90, 40m), ("M-CEDO", 30, 10m));
            long producaoId;
            await using (var db = _banco.NovoContexto())
                producaoId = (await NovoServico(db).ProduzirAsync(new BateladaRequest { ReceitaId = c.ReceitaId, VolumePlanejado = 100m }, c.UsuarioId)).Id;

            await using var leitura = _banco.NovoContexto();
            var servico = NovoServico(leitura);
            var consumos = await servico.ObterConsumosAsync(producaoId);

            Assert.Equal(2, consumos.Count);
            Assert.All(consumos, x => Assert.Equal("Malte Pilsen", x.Insumo));
            Assert.Equal(new[] { "M-CEDO", "M-TARDE" }, consumos.OrderBy(x => x.Validade).Select(x => x.CodigoLote));
            Assert.Equal(18m, consumos.Sum(x => x.Quantidade));

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterConsumosAsync(producaoId + 100));
        }

        [Fact]
        public async Task So_lote_vencido_devolve_LOTE_VENCIDO_e_o_banco_fica_intacto()
        {
            var c = await PrepararAsync(("M-VENCIDO", -2, 100m));

            await using (var db = _banco.NovoContexto())
            {
                var ex = await Assert.ThrowsAsync<LoteVencidoException>(() =>
                    NovoServico(db).ProduzirAsync(new BateladaRequest { ReceitaId = c.ReceitaId, VolumePlanejado = 100m }, c.UsuarioId));
                Assert.Equal("LOTE_VENCIDO", ex.Codigo);
            }

            await AssertSemEfeitoAsync(expectativaSaldo: 100m);
        }

        [Fact]
        public async Task Saldo_insuficiente_nao_deixa_efeito_parcial()
        {
            var c = await PrepararAsync(("M-1", 30, 10m), ("M-2", 90, 3m));   // 13 kg, precisa de 18

            await using (var db = _banco.NovoContexto())
            {
                var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() =>
                    NovoServico(db).ProduzirAsync(new BateladaRequest { ReceitaId = c.ReceitaId, VolumePlanejado = 100m }, c.UsuarioId));
                Assert.Contains("falta 5", ex.Detalhes!["Malte Pilsen"][0]);
            }

            await AssertSemEfeitoAsync(expectativaSaldo: 13m);
        }

        private async Task AssertSemEfeitoAsync(decimal expectativaSaldo)
        {
            await using var db = _banco.NovoContexto();
            var lotes = await db.LotesInsumo.AsNoTracking().ToListAsync();
            Assert.Equal(expectativaSaldo, lotes.Sum(l => l.QuantidadeAtual));
            Assert.All(lotes, l => Assert.Equal(0, l.Versao));
            Assert.Equal(0, await db.Bateladas.CountAsync());
            Assert.Equal(0, await db.BateladaConsumos.CountAsync());
            Assert.Equal(0, await db.Movimentacoes.CountAsync());
        }
    }
}
