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
    /// <summary>
    /// API-23 no PostgreSQL de verdade: a perda grava saldo, versão e movimentação PERDA com motivo,
    /// o alerta some depois da perda que zera o lote, e duas perdas ao mesmo tempo não passam do saldo.
    /// </summary>
    [Trait("Categoria", "Docker")]
    public class PerdasPostgresTests : IClassFixture<ConcorrenciaPostgresFixture>
    {
        private readonly ConcorrenciaPostgresFixture _banco;

        public PerdasPostgresTests(ConcorrenciaPostgresFixture banco) => _banco = banco;

        private class RelogioReal : IRelogio
        {
            public DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);
            public DateTime AgoraUtc => DateTime.UtcNow;
        }

        private sealed record Cenario(long InsumoId, long LoteVencidoId, long LoteBomId, long UsuarioId);

        // Insumo com um lote vencido (saldo 100) e um lote bom longe de vencer (saldo 500)
        private async Task<Cenario> PrepararAsync()
        {
            await using var db = _banco.NovoContexto();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE usuario, fornecedor, insumo, receita RESTART IDENTITY CASCADE");
            var hoje = new RelogioReal().Hoje;

            var malte = new Insumo
            {
                Nome = "Malte Pilsen", Categoria = CategoriaInsumo.MALTE, UnidadeMedida = UnidadeMedida.KG,
                EstoqueMinimo = 0, DiasValidadeCritica = 15, DiasValidadeAtencao = 45, Ativo = true
            };
            var usuario = new Usuario { Nome = "Operador", Email = "operador@teste.local", SenhaHash = "hash", Perfil = PerfilUsuario.OPERADOR };
            db.Usuarios.Add(usuario);

            var vencido = new LoteInsumo
            {
                Insumo = malte, CodigoLote = "M-VENCIDO", DataEntrada = hoje.AddDays(-90), DataValidade = hoje.AddDays(-2),
                QuantidadeInicial = 100m, QuantidadeAtual = 100m, PrecoUnitario = 8.5m
            };
            var bom = new LoteInsumo
            {
                Insumo = malte, CodigoLote = "M-BOM", DataEntrada = hoje.AddDays(-10), DataValidade = hoje.AddDays(300),
                QuantidadeInicial = 500m, QuantidadeAtual = 500m, PrecoUnitario = 9m
            };
            db.LotesInsumo.AddRange(vencido, bom);
            await db.SaveChangesAsync();

            return new Cenario(malte.Id, vencido.Id, bom.Id, usuario.Id);
        }

        private static PerdaService NovoServico(AppDbContext db) => new(new PerdaRepository(db), new RelogioReal());

        private static AlertasService NovoAlerta(AppDbContext db) =>
            new(new InsumoRepository(db), new LoteRepository(db), new RelogioReal());

        [Fact]
        public async Task Perda_grava_saldo_versao_e_movimentacao_PERDA_com_motivo()
        {
            var c = await PrepararAsync();

            await using (var db = _banco.NovoContexto())
                await NovoServico(db).RegistrarAsync(c.LoteVencidoId, new PerdaRequest { Quantidade = 30m, Motivo = "Descarte por vencimento" }, c.UsuarioId);

            await using var leitura = _banco.NovoContexto();
            var lote = await leitura.LotesInsumo.AsNoTracking().SingleAsync(l => l.Id == c.LoteVencidoId);
            Assert.Equal((70m, 1L), (lote.QuantidadeAtual, lote.Versao));

            var mov = await leitura.Movimentacoes.AsNoTracking().SingleAsync();
            Assert.Equal(TipoMovimentacao.PERDA, mov.Tipo);
            Assert.Equal(-30m, mov.Quantidade);
            Assert.Equal("Descarte por vencimento", mov.Motivo);
            Assert.Equal((c.LoteVencidoId, c.UsuarioId), (mov.LoteInsumoId, mov.UsuarioId));
        }

        [Fact]
        public async Task Alerta_de_lote_vencido_aparece_e_some_depois_da_perda_que_zera_o_lote()
        {
            var c = await PrepararAsync();

            await using (var db = _banco.NovoContexto())
            {
                var antes = await NovoAlerta(db).ListarAsync(null, null, null);
                var alerta = Assert.Single(antes);
                Assert.Equal((TipoAlerta.VENCIDO, AcaoAlerta.REGISTRAR_PERDA, c.InsumoId, c.LoteVencidoId),
                    (alerta.Tipo, alerta.Acao, alerta.InsumoId, alerta.LoteId!.Value));
                Assert.Equal(100m, alerta.Saldo);
            }

            await using (var db = _banco.NovoContexto())
                await NovoServico(db).RegistrarAsync(c.LoteVencidoId, new PerdaRequest { Quantidade = 100m, Motivo = "Descarte total" }, c.UsuarioId);

            await using (var db = _banco.NovoContexto())
                Assert.Empty(await NovoAlerta(db).ListarAsync(null, null, null));
        }

        [Fact]
        public async Task Perda_maior_que_o_saldo_nao_altera_o_banco()
        {
            var c = await PrepararAsync();

            await using (var db = _banco.NovoContexto())
            {
                var ex = await Assert.ThrowsAsync<NegocioException>(() =>
                    NovoServico(db).RegistrarAsync(c.LoteVencidoId, new PerdaRequest { Quantidade = 100.001m, Motivo = "Perda grande demais" }, c.UsuarioId));
                Assert.Equal("PERDA_MAIOR_QUE_SALDO", ex.Codigo);
            }

            await using var leitura = _banco.NovoContexto();
            var lote = await leitura.LotesInsumo.AsNoTracking().SingleAsync(l => l.Id == c.LoteVencidoId);
            Assert.Equal((100m, 0L), (lote.QuantidadeAtual, lote.Versao));
            Assert.Equal(0, await leitura.Movimentacoes.CountAsync());
        }

        [Fact]
        public async Task Lote_inexistente_devolve_404_no_banco_real()
        {
            await PrepararAsync();

            await using var db = _banco.NovoContexto();
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
                NovoServico(db).RegistrarAsync(99999, new PerdaRequest { Quantidade = 1m, Motivo = "Lote que não existe" }, 1));
        }

        [Fact]
        public async Task Duas_perdas_ao_mesmo_tempo_nao_passam_do_saldo()
        {
            for (var repeticao = 1; repeticao <= 10; repeticao++)
            {
                var c = await PrepararAsync();   // lote vencido com saldo 100

                var largada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var prontas = new[] { new TaskCompletionSource(), new TaskCompletionSource() };

                var tarefas = Enumerable.Range(0, 2).Select(i => Task.Run(async () =>
                {
                    await using var db = _banco.NovoContexto();
                    await db.Database.OpenConnectionAsync();
                    var servico = NovoServico(db);
                    prontas[i].SetResult();
                    await largada.Task;
                    try
                    {
                        await servico.RegistrarAsync(c.LoteVencidoId, new PerdaRequest { Quantidade = 60m, Motivo = $"Perda simultânea {i}" }, c.UsuarioId);
                        return (Sucesso: true, Erro: (Exception?)null);
                    }
                    catch (Exception ex)
                    {
                        return (Sucesso: false, Erro: ex);
                    }
                })).ToList();

                await Task.WhenAll(prontas.Select(p => p.Task)).WaitAsync(TimeSpan.FromSeconds(60));
                largada.SetResult();
                var resultados = await Task.WhenAll(tarefas).WaitAsync(TimeSpan.FromSeconds(60));

                Assert.True(resultados.Count(r => r.Sucesso) == 1, $"Esperava 1 sucesso e houve {resultados.Count(r => r.Sucesso)} (repetição {repeticao}).");
                var falha = Assert.IsType<NegocioException>(resultados.Single(r => !r.Sucesso).Erro);
                Assert.Equal("PERDA_MAIOR_QUE_SALDO", falha.Codigo);

                await using var leitura = _banco.NovoContexto();
                var lote = await leitura.LotesInsumo.AsNoTracking().SingleAsync(l => l.Id == c.LoteVencidoId);
                Assert.Equal((40m, 1L), (lote.QuantidadeAtual, lote.Versao));
                Assert.Equal(1, await leitura.Movimentacoes.CountAsync());
            }
        }
    }
}
