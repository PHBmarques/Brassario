using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>
    /// Sobe um PostgreSQL de verdade (Docker) e aplica as migrations reais, com os CHECK e o FOR UPDATE.
    /// O banco em memória não executa nenhum dos dois, por isso a concorrência só se prova aqui.
    /// </summary>
    public class ConcorrenciaPostgresFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

        public string ConnectionString => _postgres.GetConnectionString() + ";Maximum Pool Size=60";

        public async Task InitializeAsync()
        {
            await _postgres.StartAsync();
            await using var db = NovoContexto();
            await db.Database.MigrateAsync();
        }

        public async Task DisposeAsync() => await _postgres.DisposeAsync();

        public AppDbContext NovoContexto() =>
            new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
    }

    /// <summary>TST-4: CT-23 e CT-24, produções simultâneas disputando o mesmo saldo (PostgreSQL real).</summary>
    [Trait("Categoria", "Docker")]
    public class ConcorrenciaPostgresTests : IClassFixture<ConcorrenciaPostgresFixture>
    {
        private const decimal PorBatelada = 18m;   // malte necessário para 100 L
        private static readonly TimeSpan Limite = TimeSpan.FromSeconds(90);

        // Repetições de cada cenário (a spec sugere 50). Para uma rodada rápida: TST4_REPETICOES=5
        private static int Repeticoes =>
            int.TryParse(Environment.GetEnvironmentVariable("TST4_REPETICOES"), out var n) && n > 0 ? n : 50;

        private readonly ConcorrenciaPostgresFixture _banco;

        public ConcorrenciaPostgresTests(ConcorrenciaPostgresFixture banco) => _banco = banco;

        // ---------- Apoio ----------

        private class RelogioReal : IRelogio
        {
            public DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);
            public DateTime AgoraUtc => DateTime.UtcNow;
        }

        /// <summary>Lê a receita direto do banco, como o ReceitaService faz na API.</summary>
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

        private sealed record Resultado(bool Sucesso, BateladaResponse? Resposta, Exception? Erro);

        private sealed record Cenario(long ReceitaId, List<long> UsuarioIds, decimal SaldoInicial);

        /// <summary>Limpa o banco e monta: 1 receita (18 kg de malte por 100 L), N usuários e os lotes de malte.</summary>
        private async Task<Cenario> PrepararAsync(int usuarios, params decimal[] saldosDosLotes)
        {
            await using var db = _banco.NovoContexto();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE usuario, fornecedor, insumo, receita RESTART IDENTITY CASCADE");

            var hoje = new RelogioReal().Hoje;

            var malte = new Insumo
            {
                Nome = "Malte Pilsen",
                Categoria = CategoriaInsumo.MALTE,
                UnidadeMedida = UnidadeMedida.KG,
                EstoqueMinimo = 0,
                DiasValidadeCritica = 15,
                DiasValidadeAtencao = 45,
                Ativo = true
            };

            var receita = new Receita { Nome = "IPA Cascade", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { Insumo = malte, Quantidade = PorBatelada });
            db.Receitas.Add(receita);

            var donos = Enumerable.Range(1, usuarios).Select(i => new Usuario
            {
                Nome = $"Operador {i}",
                Email = $"operador{i}@teste.local",
                SenhaHash = "hash-de-teste",
                Perfil = PerfilUsuario.OPERADOR
            }).ToList();
            db.Usuarios.AddRange(donos);

            for (var i = 0; i < saldosDosLotes.Length; i++)
            {
                db.LotesInsumo.Add(new LoteInsumo
                {
                    Insumo = malte,
                    CodigoLote = $"M-{i + 1}",
                    DataEntrada = hoje,
                    DataValidade = hoje.AddDays(30 * (i + 1)),   // o lote 1 vence primeiro (FEFO)
                    QuantidadeInicial = saldosDosLotes[i],
                    QuantidadeAtual = saldosDosLotes[i],
                    PrecoUnitario = 8.5m
                });
            }

            await db.SaveChangesAsync();
            return new Cenario(receita.Id, donos.Select(u => u.Id).ToList(), saldosDosLotes.Sum());
        }

        /// <summary>
        /// Dispara as produções ao mesmo tempo: cada tarefa monta o seu contexto, abre a conexão, avisa que está pronta
        /// e espera a largada. Cada uma usa um usuário diferente.
        /// </summary>
        private static async Task<List<Resultado>> DispararAsync(ConcorrenciaPostgresFixture banco, Cenario cenario)
        {
            var n = cenario.UsuarioIds.Count;
            var prontas = Enumerable.Range(0, n).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            var largada = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var tarefas = Enumerable.Range(0, n).Select(i => Task.Run(async () =>
            {
                await using var db = banco.NovoContexto();
                await db.Database.OpenConnectionAsync();
                var servico = new BateladaService(new ReceitaDoBanco(db), new BateladaRepository(db), new RelogioReal());
                var pedido = new BateladaRequest { ReceitaId = cenario.ReceitaId, VolumePlanejado = 100m };

                prontas[i].SetResult();
                await largada.Task;

                try
                {
                    return new Resultado(true, await servico.ProduzirAsync(pedido, cenario.UsuarioIds[i]), null);
                }
                catch (Exception ex)
                {
                    return new Resultado(false, null, ex);
                }
            })).ToList();

            await Task.WhenAll(prontas.Select(p => p.Task)).WaitAsync(Limite);
            largada.SetResult();
            return (await Task.WhenAll(tarefas).WaitAsync(Limite)).ToList();
        }

        /// <summary>Confere os resultados das respostas e o estado final do banco.</summary>
        private async Task ConferirAsync(Cenario cenario, List<Resultado> resultados, int sucessosEsperados, int repeticao)
        {
            var rotulo = $"(repetição {repeticao})";
            var sucessos = resultados.Count(r => r.Sucesso);
            var consumidoEsperado = sucessosEsperados * PorBatelada;

            // 1) Respostas: só os sucessos que o saldo comporta; as demais são 422 ESTOQUE_INSUFICIENTE (nunca outro erro)
            Assert.True(sucessosEsperados == sucessos, $"Esperava {sucessosEsperados} sucesso(s) e houve {sucessos} {rotulo}.");
            foreach (var falha in resultados.Where(r => !r.Sucesso))
            {
                var estoque = Assert.IsType<EstoqueInsuficienteException>(falha.Erro);
                Assert.Equal("ESTOQUE_INSUFICIENTE", estoque.Codigo);
            }

            // 2) Banco
            await using var db = _banco.NovoContexto();
            var lotes = await db.LotesInsumo.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
            var consumos = await db.BateladaConsumos.AsNoTracking().ToListAsync();
            var movimentacoes = await db.Movimentacoes.AsNoTracking().ToListAsync();
            var bateladas = await db.Bateladas.AsNoTracking().ToListAsync();

            // saldo nunca negativo e igual ao esperado
            Assert.All(lotes, l => Assert.True(l.QuantidadeAtual >= 0, $"Saldo negativo no lote {l.CodigoLote} {rotulo}."));
            Assert.Equal(cenario.SaldoInicial - consumidoEsperado, lotes.Sum(l => l.QuantidadeAtual));

            // soma dos consumos = saldo inicial menos saldo final, no total e lote a lote (rastreável)
            Assert.Equal(consumidoEsperado, consumos.Sum(c => c.Quantidade));
            Assert.Equal(cenario.SaldoInicial - lotes.Sum(l => l.QuantidadeAtual), consumos.Sum(c => c.Quantidade));
            foreach (var lote in lotes)
            {
                var doLote = consumos.Where(c => c.LoteInsumoId == lote.Id).ToList();
                Assert.Equal(lote.QuantidadeInicial - lote.QuantidadeAtual, doLote.Sum(c => c.Quantidade));
                Assert.Equal((long)doLote.Count, lote.Versao);   // cada baixa incrementa a versão uma vez
            }

            // bateladas: uma por sucesso, códigos únicos e em sequência
            Assert.Equal(sucessosEsperados, bateladas.Count);
            var codigos = bateladas.Select(b => b.Codigo).OrderBy(c => c).ToList();
            Assert.Equal(codigos.Distinct().Count(), codigos.Count);
            var ano = new RelogioReal().Hoje.Year;
            Assert.Equal(Enumerable.Range(1, sucessosEsperados).Select(i => $"B-{ano}-{i:0000}"), codigos);
            Assert.Equal(sucessosEsperados, bateladas.Select(b => b.UsuarioId).Distinct().Count());   // cada sucesso é de um usuário

            // movimentações coerentes: uma saída negativa por consumo, ligada à batelada
            Assert.Equal(consumos.Count, movimentacoes.Count);
            Assert.All(movimentacoes, m =>
            {
                Assert.Equal(TipoMovimentacao.SAIDA_PRODUCAO, m.Tipo);
                Assert.NotNull(m.BateladaId);
                Assert.True(m.Quantidade < 0);
            });
            Assert.Equal(-consumidoEsperado, movimentacoes.Sum(m => m.Quantidade));
        }

        // ---------- CT-23: duas produções simultâneas, saldo para só uma ----------

        [Fact]
        public async Task CT23_duas_producoes_simultaneas_com_saldo_para_uma_dao_1_sucesso_e_1_falha()
        {
            for (var rodada = 1; rodada <= Repeticoes; rodada++)
            {
                var cenario = await PrepararAsync(usuarios: 2, saldosDosLotes: 20m);   // 20 kg: cabe 1 batelada de 18 kg

                var resultados = await DispararAsync(_banco, cenario);

                Assert.Single(resultados, r => r.Sucesso);
                Assert.Single(resultados, r => !r.Sucesso);
                await ConferirAsync(cenario, resultados, sucessosEsperados: 1, repeticao: rodada);

                await using var db = _banco.NovoContexto();
                Assert.Equal(2m, (await db.LotesInsumo.SingleAsync()).QuantidadeAtual);   // 20 - 18
            }
        }

        // ---------- CT-24: muitas produções simultâneas ----------

        [Fact]
        public async Task CT24_dez_producoes_simultaneas_com_saldo_para_tres_dao_exatamente_3_sucessos()
        {
            for (var rodada = 1; rodada <= Repeticoes; rodada++)
            {
                var cenario = await PrepararAsync(usuarios: 10, saldosDosLotes: 55m);   // 55 kg: cabem 3 bateladas, sobra 1

                var resultados = await DispararAsync(_banco, cenario);

                Assert.Equal(3, resultados.Count(r => r.Sucesso));
                Assert.Equal(7, resultados.Count(r => !r.Sucesso));
                await ConferirAsync(cenario, resultados, sucessosEsperados: 3, repeticao: rodada);

                await using var db = _banco.NovoContexto();
                Assert.Equal(1m, (await db.LotesInsumo.SingleAsync()).QuantidadeAtual);   // 55 - 54
            }
        }

        [Fact]
        public async Task CT24_com_o_saldo_dividido_em_dois_lotes_o_consumo_segue_o_FEFO_sem_erro_de_conta()
        {
            for (var rodada = 1; rodada <= Repeticoes; rodada++)
            {
                // 30 kg no lote que vence primeiro e 25 kg no outro: algumas bateladas passam pelos dois lotes
                var cenario = await PrepararAsync(usuarios: 10, saldosDosLotes: new[] { 30m, 25m });

                var resultados = await DispararAsync(_banco, cenario);

                Assert.Equal(3, resultados.Count(r => r.Sucesso));
                await ConferirAsync(cenario, resultados, sucessosEsperados: 3, repeticao: rodada);

                // FEFO: o lote que vence primeiro foi esvaziado antes de tocar no segundo
                await using var db = _banco.NovoContexto();
                var lotes = await db.LotesInsumo.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
                Assert.Equal(0m, lotes[0].QuantidadeAtual);    // 30 kg consumidos por inteiro
                Assert.Equal(1m, lotes[1].QuantidadeAtual);    // 54 - 30 = 24 saíram do segundo: 25 - 24
            }
        }
    }
}