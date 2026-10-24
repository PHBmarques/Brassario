using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>
    /// API-22 no PostgreSQL de verdade: filtro por período, paginação, filtros por receita e status,
    /// e as movimentações de um lote que passou por duas produções.
    /// </summary>
    [Trait("Categoria", "Docker")]
    public class HistoricoPostgresTests : IClassFixture<ConcorrenciaPostgresFixture>
    {
        private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");

        private readonly ConcorrenciaPostgresFixture _banco;

        public HistoricoPostgresTests(ConcorrenciaPostgresFixture banco) => _banco = banco;

        private HistoricoService NovoServico(AppDbContext db) => new(new HistoricoRepository(db), Brasilia);

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

        private sealed record Base(long ReceitaId, long Receita2Id, long Usuario1Id, long Usuario2Id, long InsumoId);

        // Limpa o banco e cria 2 receitas (18 kg de malte por 100 L), 2 usuários e o insumo
        private async Task<Base> PrepararBaseAsync(AppDbContext db)
        {
            await db.Database.ExecuteSqlRawAsync("TRUNCATE usuario, fornecedor, insumo, receita RESTART IDENTITY CASCADE");

            var malte = new Insumo
            {
                Nome = "Malte Pilsen", Categoria = CategoriaInsumo.MALTE, UnidadeMedida = UnidadeMedida.KG,
                EstoqueMinimo = 0, DiasValidadeCritica = 15, DiasValidadeAtencao = 45, Ativo = true
            };
            var ipa = new Receita { Nome = "IPA Cascade", VolumeBaseLitros = 100, Ativa = true };
            ipa.Itens.Add(new ReceitaItem { Insumo = malte, Quantidade = 18m });
            var pilsen = new Receita { Nome = "Pilsen", VolumeBaseLitros = 100, Ativa = true };
            pilsen.Itens.Add(new ReceitaItem { Insumo = malte, Quantidade = 20m });
            db.Receitas.AddRange(ipa, pilsen);

            var ana = new Usuario { Nome = "Ana Souza", Email = "ana@teste.local", SenhaHash = "hash", Perfil = PerfilUsuario.OPERADOR };
            var bruno = new Usuario { Nome = "Bruno Lima", Email = "bruno@teste.local", SenhaHash = "hash", Perfil = PerfilUsuario.OPERADOR };
            db.Usuarios.AddRange(ana, bruno);

            await db.SaveChangesAsync();
            return new Base(ipa.Id, pilsen.Id, ana.Id, bruno.Id, malte.Id);
        }

        private static Batelada Producao(Base b, string codigo, DateTime dataUtc, long? receitaId = null,
            StatusBatelada status = StatusBatelada.FINALIZADA, decimal custo = 100m) => new()
        {
            Codigo = codigo, ReceitaId = receitaId ?? b.ReceitaId, UsuarioId = b.Usuario1Id,
            VolumePlanejado = 100m, Status = status, CustoTotal = custo, DataProducao = dataUtc
        };

        private static DateTime Utc(int mes, int dia, int hora, int minuto = 0) => new(2026, mes, dia, hora, minuto, 0, DateTimeKind.Utc);

        // ---------- Período ----------

        [Fact]
        public async Task Filtro_por_periodo_usa_o_dia_do_calendario_local_inclusive_nos_dois_lados()
        {
            await using (var db = _banco.NovoContexto())
            {
                var b = await PrepararBaseAsync(db);
                db.Bateladas.AddRange(
                    Producao(b, "B-2026-0001", Utc(10, 10, 12)),
                    Producao(b, "B-2026-0002", Utc(10, 20, 12)),
                    Producao(b, "B-2026-0003", Utc(10, 21, 2, 30)),   // 20/10 23:30 em Brasília
                    Producao(b, "B-2026-0004", Utc(10, 21, 12)),
                    Producao(b, "B-2026-0005", Utc(10, 22, 12)));
                await db.SaveChangesAsync();
            }

            await using var leitura = _banco.NovoContexto();
            var servico = NovoServico(leitura);

            async Task<string[]> Codigos(DateOnly? de, DateOnly? ate) =>
                (await servico.ListarBateladasAsync(de, ate, null, null, null, null)).Itens.Select(i => i.Codigo).ToArray();

            Assert.Equal(new[] { "B-2026-0003", "B-2026-0002" }, await Codigos(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20)));
            Assert.Equal(new[] { "B-2026-0005", "B-2026-0004" }, await Codigos(new DateOnly(2026, 10, 21), null));
            Assert.Equal(new[] { "B-2026-0001" }, await Codigos(null, new DateOnly(2026, 10, 19)));
            Assert.Equal(new[] { "B-2026-0004", "B-2026-0003", "B-2026-0002" }, await Codigos(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 21)));
            Assert.Empty(await Codigos(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
            Assert.Equal(5, (await Codigos(null, null)).Length);
        }

        // ---------- Paginação ----------

        [Fact]
        public async Task Paginacao_devolve_as_mais_recentes_primeiro_sem_repetir_nem_pular_linhas()
        {
            await using (var db = _banco.NovoContexto())
            {
                var b = await PrepararBaseAsync(db);
                db.Bateladas.AddRange(
                    Producao(b, "B-2026-0001", Utc(10, 10, 12)),
                    Producao(b, "B-2026-0002", Utc(10, 20, 12)),
                    Producao(b, "B-2026-0003", Utc(10, 21, 2, 30)),
                    Producao(b, "B-2026-0004", Utc(10, 21, 12)),
                    Producao(b, "B-2026-0005", Utc(10, 22, 12)));
                await db.SaveChangesAsync();
            }

            await using var leitura = _banco.NovoContexto();
            var servico = NovoServico(leitura);

            var p1 = await servico.ListarBateladasAsync(null, null, null, null, 1, 2);
            var p2 = await servico.ListarBateladasAsync(null, null, null, null, 2, 2);
            var p3 = await servico.ListarBateladasAsync(null, null, null, null, 3, 2);
            var p4 = await servico.ListarBateladasAsync(null, null, null, null, 4, 2);

            Assert.Equal(new[] { "B-2026-0005", "B-2026-0004" }, p1.Itens.Select(i => i.Codigo));
            Assert.Equal(new[] { "B-2026-0003", "B-2026-0002" }, p2.Itens.Select(i => i.Codigo));
            Assert.Equal(new[] { "B-2026-0001" }, p3.Itens.Select(i => i.Codigo));
            Assert.Empty(p4.Itens);

            Assert.All(new[] { p1, p2, p3, p4 }, p => Assert.Equal((5, 3, 2), (p.Total, p.TotalPaginas, p.TamanhoPagina)));
        }

        [Fact]
        public async Task Producoes_no_mesmo_instante_nao_se_repetem_entre_paginas()
        {
            await using (var db = _banco.NovoContexto())
            {
                var b = await PrepararBaseAsync(db);
                for (var i = 1; i <= 5; i++)
                    db.Bateladas.Add(Producao(b, $"B-2026-000{i}", Utc(10, 20, 12)));   // todas no mesmo instante
                await db.SaveChangesAsync();
            }

            await using var leitura = _banco.NovoContexto();
            var servico = NovoServico(leitura);

            var codigos = new List<string>();
            for (var pagina = 1; pagina <= 3; pagina++)
                codigos.AddRange((await servico.ListarBateladasAsync(null, null, null, null, pagina, 2)).Itens.Select(i => i.Codigo));

            Assert.Equal(5, codigos.Distinct().Count());
            Assert.Equal(new[] { "B-2026-0005", "B-2026-0004", "B-2026-0003", "B-2026-0002", "B-2026-0001" }, codigos);
        }

        // ---------- Receita e status ----------

        [Fact]
        public async Task Filtros_por_receita_e_por_status_e_a_linha_traz_receita_e_responsavel()
        {
            long ipaId, pilsenId;
            await using (var db = _banco.NovoContexto())
            {
                var b = await PrepararBaseAsync(db);
                ipaId = b.ReceitaId;
                pilsenId = b.Receita2Id;
                db.Bateladas.AddRange(
                    Producao(b, "B-2026-0001", Utc(10, 10, 12), b.ReceitaId, StatusBatelada.FINALIZADA, 150m),
                    Producao(b, "B-2026-0002", Utc(10, 11, 12), b.Receita2Id, StatusBatelada.EM_ANDAMENTO, 160m),
                    Producao(b, "B-2026-0003", Utc(10, 12, 12), b.ReceitaId, StatusBatelada.CANCELADA, 0m),
                    Producao(b, "B-2026-0004", Utc(10, 13, 12), b.Receita2Id, StatusBatelada.FINALIZADA, 170m));
                await db.SaveChangesAsync();
            }

            await using var leitura = _banco.NovoContexto();
            var servico = NovoServico(leitura);

            var daIpa = await servico.ListarBateladasAsync(null, null, ipaId, null, null, null);
            Assert.Equal(new[] { "B-2026-0003", "B-2026-0001" }, daIpa.Itens.Select(i => i.Codigo));
            Assert.All(daIpa.Itens, i => Assert.Equal("IPA Cascade", i.Receita));

            var finalizadas = await servico.ListarBateladasAsync(null, null, null, "FINALIZADA", null, null);
            Assert.Equal(new[] { "B-2026-0004", "B-2026-0001" }, finalizadas.Itens.Select(i => i.Codigo));

            var pilsenAndamento = await servico.ListarBateladasAsync(null, null, pilsenId, "em_andamento", null, null);
            var unica = Assert.Single(pilsenAndamento.Itens);
            Assert.Equal(("B-2026-0002", "Pilsen", "Ana Souza", 160m, 100m, StatusBatelada.EM_ANDAMENTO),
                (unica.Codigo, unica.Receita, unica.Responsavel, unica.CustoTotal, unica.Quantidade, unica.Status));

            Assert.Empty((await servico.ListarBateladasAsync(null, null, 999, null, null, null)).Itens);
        }

        // ---------- Movimentações de um lote que passou por duas produções ----------

        [Fact]
        public async Task Lote_que_passou_por_duas_producoes_mostra_a_entrada_e_as_duas_saidas()
        {
            long receitaId, insumoId, loteId, ana, bruno;
            await using (var db = _banco.NovoContexto())
            {
                var b = await PrepararBaseAsync(db);
                receitaId = b.ReceitaId; insumoId = b.InsumoId; ana = b.Usuario1Id; bruno = b.Usuario2Id;

                var lote = new LoteInsumo
                {
                    InsumoId = b.InsumoId, CodigoLote = "M-1",
                    DataEntrada = new RelogioReal().Hoje, DataValidade = new RelogioReal().Hoje.AddDays(90),
                    QuantidadeInicial = 100m, QuantidadeAtual = 100m, PrecoUnitario = 8.5m
                };
                db.LotesInsumo.Add(lote);
                await db.SaveChangesAsync();
                loteId = lote.Id;

                db.Movimentacoes.Add(new MovimentacaoEstoque
                {
                    LoteInsumoId = lote.Id, UsuarioId = b.Usuario1Id, Tipo = TipoMovimentacao.ENTRADA,
                    Quantidade = 100m, DataHora = DateTime.UtcNow.AddMinutes(-5)
                });
                await db.SaveChangesAsync();
            }

            long primeira, segunda;
            await using (var db = _banco.NovoContexto())
                primeira = (await new BateladaService(new ReceitaDoBanco(db), new BateladaRepository(db), new RelogioReal())
                    .ProduzirAsync(new BateladaRequest { ReceitaId = receitaId, VolumePlanejado = 100m }, ana)).Id;
            await using (var db = _banco.NovoContexto())
                segunda = (await new BateladaService(new ReceitaDoBanco(db), new BateladaRepository(db), new RelogioReal())
                    .ProduzirAsync(new BateladaRequest { ReceitaId = receitaId, VolumePlanejado = 100m }, bruno)).Id;

            await using var leitura = _banco.NovoContexto();
            var servico = NovoServico(leitura);

            // Do lote: a saída mais recente primeiro e a entrada por último
            var doLote = await servico.ListarMovimentacoesAsync(null, loteId, null, null, null);
            Assert.Equal(3, doLote.Total);
            Assert.Equal(new[] { TipoMovimentacao.SAIDA_PRODUCAO, TipoMovimentacao.SAIDA_PRODUCAO, TipoMovimentacao.ENTRADA },
                doLote.Itens.Select(m => m.Tipo));
            Assert.Equal(new[] { -18m, -18m, 100m }, doLote.Itens.Select(m => m.Quantidade));
            Assert.Equal(new long?[] { segunda, primeira, null }, doLote.Itens.Select(m => m.ProducaoId));
            Assert.Equal(new[] { "Bruno Lima", "Ana Souza", "Ana Souza" }, doLote.Itens.Select(m => m.Responsavel));
            Assert.All(doLote.Itens, m => Assert.Equal(("M-1", "Malte Pilsen", UnidadeMedida.KG), (m.CodigoLote, m.Insumo, m.UnidadeMedida)));
            Assert.StartsWith("B-", doLote.Itens[0].CodigoProducao);
            Assert.Equal(64m, doLote.Itens.Sum(m => m.Quantidade));   // 100 de entrada - 18 - 18 = saldo do lote

            // Por insumo: as mesmas três
            Assert.Equal(3, (await servico.ListarMovimentacoesAsync(insumoId, null, null, null, null)).Total);

            // Por produção: só a saída daquela produção
            var daPrimeira = Assert.Single((await servico.ListarMovimentacoesAsync(null, null, primeira, null, null)).Itens);
            Assert.Equal((primeira, -18m), (daPrimeira.ProducaoId, daPrimeira.Quantidade));

            // Paginação das movimentações
            var pagina = await servico.ListarMovimentacoesAsync(null, loteId, null, 2, 2);
            Assert.Equal(new[] { TipoMovimentacao.ENTRADA }, pagina.Itens.Select(m => m.Tipo));
            Assert.Equal((3, 2), (pagina.Total, pagina.TotalPaginas));

            // Lote ou insumo que não existe: lista vazia, não erro
            Assert.Equal(0, (await servico.ListarMovimentacoesAsync(null, 99999, null, null, null)).Total);
        }
    }
}
