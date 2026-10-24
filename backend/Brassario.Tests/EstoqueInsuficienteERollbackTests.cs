using System.Text.Json;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Middleware;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>
    /// TST-3: estoque insuficiente (CT-21), lote vencido (CT-22) e rollback (CT-25) na produção de batelada.
    /// O repositório falso imita a transação (restaura os saldos se a operação lançar exceção).
    /// O rollback de verdade, no PostgreSQL, é o assunto do TST-4.
    /// </summary>
    public class EstoqueInsuficienteERollbackTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 20);
        private const long Usuario = 7;

        // ---------- Apoio (repositório falso com transação e gancho de falha) ----------

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => EstoqueInsuficienteERollbackTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        }

        private class ReceitasStub : IReceitaService
        {
            public Receita Receita { get; set; } = null!;

            public Task<Receita> ObterParaProducaoAsync(long id, CancellationToken ct = default) => Task.FromResult(Receita);
            public Task<List<ReceitaResumoResponse>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> ObterAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> CriarAsync(ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AtualizarAsync(long id, ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AlterarStatusAsync(long id, bool ativa, CancellationToken ct = default) => throw new NotSupportedException();
        }

        private class BateladasFalso : IBateladaRepository
        {
            public List<LoteInsumo> Lotes { get; } = new();
            public List<Batelada> Gravadas { get; } = new();
            private readonly List<Batelada> _pendentes = new();
            public int Salvamentos { get; private set; }

            /// <summary>Roda dentro de AdicionarAsync, quando a baixa dos saldos já foi aplicada na memória.</summary>
            public Action? AoAdicionar { get; set; }
            public bool FalharAoSalvar { get; set; }

            public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default)
            {
                var copia = Lotes.Select(l => (l, l.QuantidadeAtual, l.Versao)).ToList();
                try
                {
                    return await operacao();
                }
                catch
                {
                    foreach (var (lote, saldo, versao) in copia)
                    {
                        lote.QuantidadeAtual = saldo;
                        lote.Versao = versao;
                    }
                    _pendentes.Clear();
                    throw;
                }
            }

            public Task<List<LoteInsumo>> BloquearLotesAsync(IReadOnlyCollection<long> insumoIds, CancellationToken ct = default) =>
                Task.FromResult(Lotes
                    .Where(l => insumoIds.Contains(l.InsumoId) && l.QuantidadeAtual > 0)
                    .OrderBy(l => l.InsumoId).ThenBy(l => l.DataValidade).ThenBy(l => l.DataEntrada).ThenBy(l => l.Id)
                    .ToList());

            public Task<Batelada?> BloquearBateladaAsync(long id, CancellationToken ct = default) =>
                Task.FromResult(Gravadas.SingleOrDefault(b => b.Id == id));

            public Task<List<LoteInsumo>> BloquearLotesPorIdAsync(IReadOnlyCollection<long> loteIds, CancellationToken ct = default) =>
                Task.FromResult(Lotes.Where(l => loteIds.Contains(l.Id)).OrderBy(l => l.Id).ToList());

            public Task<string> ProximoCodigoAsync(int ano, CancellationToken ct = default) =>
                Task.FromResult($"B-{ano}-{Gravadas.Count + 1:0000}");

            public Task AdicionarAsync(Batelada batelada, CancellationToken ct = default)
            {
                AoAdicionar?.Invoke();
                _pendentes.Add(batelada);
                return Task.CompletedTask;
            }

            public Task SalvarAsync(CancellationToken ct = default)
            {
                if (FalharAoSalvar) throw new InvalidOperationException("Falha simulada ao gravar.");
                Salvamentos++;
                foreach (var b in _pendentes)
                {
                    b.Id = Gravadas.Count + 1;
                    Gravadas.Add(b);
                }
                _pendentes.Clear();
                return Task.CompletedTask;
            }
        }

        // IPA com 3 insumos (base 100 L): 18 kg de Malte (1), 0,3 kg de Lúpulo (2) e 0,1 kg de Levedura (3)
        private static Receita NovaReceita()
        {
            var receita = new Receita { Id = 3, Nome = "IPA Cascade", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { InsumoId = 1, Insumo = new Insumo { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG }, Quantidade = 18m });
            receita.Itens.Add(new ReceitaItem { InsumoId = 2, Insumo = new Insumo { Id = 2, Nome = "Lúpulo Cascade", UnidadeMedida = UnidadeMedida.KG }, Quantidade = 0.3m });
            receita.Itens.Add(new ReceitaItem { InsumoId = 3, Insumo = new Insumo { Id = 3, Nome = "Levedura US-05", UnidadeMedida = UnidadeMedida.KG }, Quantidade = 0.1m });
            return receita;
        }

        private static (BateladaService Servico, BateladasFalso Repo) Criar()
        {
            var repo = new BateladasFalso();
            return (new BateladaService(new ReceitasStub { Receita = NovaReceita() }, repo, new RelogioFixo()), repo);
        }

        private static LoteInsumo Lote(long id, long insumoId, string codigo, DateOnly validade, decimal saldo, decimal preco = 10m) => new()
        {
            Id = id,
            InsumoId = insumoId,
            CodigoLote = codigo,
            DataEntrada = Hoje.AddDays(-30),
            DataValidade = validade,
            QuantidadeInicial = saldo,
            QuantidadeAtual = saldo,
            PrecoUnitario = preco
        };

        private static BateladaRequest Pedido(decimal volume = 100) => new() { ReceitaId = 3, VolumePlanejado = volume };

        private static Dictionary<long, (decimal Saldo, long Versao)> Foto(BateladasFalso repo) =>
            repo.Lotes.ToDictionary(l => l.Id, l => (l.QuantidadeAtual, l.Versao));

        // Passa a exceção do serviço pelo middleware de erros, como acontece na API
        private static async Task<(int Status, JsonElement Corpo)> ChamarPelaApi(Func<Task> acao)
        {
            var contexto = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            contexto.Response.Body = new MemoryStream();

            var middleware = new ExceptionHandlingMiddleware(_ => acao(), NullLogger<ExceptionHandlingMiddleware>.Instance);
            await middleware.InvokeAsync(contexto);

            contexto.Response.Body.Position = 0;
            var texto = await new StreamReader(contexto.Response.Body).ReadToEndAsync();
            return (contexto.Response.StatusCode, JsonDocument.Parse(texto).RootElement);
        }

        // ---------- CT-21: estoque insuficiente (falta 1 de 3 insumos) ----------

        [Fact]
        public async Task CT21_falta_de_1_de_3_insumos_devolve_ESTOQUE_INSUFICIENTE_e_nenhum_saldo_muda()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));      // malte de sobra
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 0.12m));    // lúpulo: precisa de 0,3
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));       // levedura de sobra
            var antes = Foto(repo);

            var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal("ESTOQUE_INSUFICIENTE", ex.Codigo);
            Assert.Single(ex.Detalhes!);
            Assert.True(ex.Detalhes!.ContainsKey("Lúpulo Cascade"));

            Assert.Equal(antes, Foto(repo));    // nenhum saldo e nenhuma versão mudou, nem dos insumos que tinham estoque
            Assert.Empty(repo.Gravadas);
            Assert.Equal(0, repo.Salvamentos);
        }

        [Fact]
        public async Task CT21_pela_API_responde_422_com_o_insumo_em_falta_nos_detalhes()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 0.12m));
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));

            var (status, corpo) = await ChamarPelaApi(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal(422, status);
            Assert.Equal("ESTOQUE_INSUFICIENTE", corpo.GetProperty("codigo").GetString());
            Assert.True(corpo.GetProperty("detalhes").TryGetProperty("Lúpulo Cascade", out _));
        }

        [Fact]
        public async Task CT21_nao_cria_batelada_consumo_nem_movimentacao()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 0.12m));
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));

            await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Empty(repo.Gravadas);
            Assert.All(repo.Lotes, l => Assert.Equal(l.QuantidadeInicial, l.QuantidadeAtual));
        }

        [Fact]
        public async Task CT21_depois_de_repor_o_estoque_a_mesma_producao_funciona()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 0.12m));
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));
            await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            repo.Lotes.Add(Lote(4, 2, "L-2", Hoje.AddDays(120), 1m));   // chega mais lúpulo
            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            Assert.Equal(3, r.Consumos.Select(c => c.InsumoId).Distinct().Count());   // os 3 insumos foram consumidos
            Assert.Equal(2, r.Consumos.Count(c => c.InsumoId == 2));                  // lúpulo saiu dos dois lotes (0,12 + 0,18)
            Assert.Single(repo.Gravadas);
        }

        // ---------- CT-22: lote vencido não é usado ----------

        [Fact]
        public async Task CT22_unico_lote_vencido_devolve_ESTOQUE_INSUFICIENTE_com_disponivel_zero()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-VENCIDO", Hoje.AddDays(-1), 100m));   // muito saldo, mas vencido
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 1m));
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));
            var antes = Foto(repo);

            var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal("ESTOQUE_INSUFICIENTE", ex.Codigo);
            Assert.Single(ex.Detalhes!);
            Assert.Contains("disponível 0", ex.Detalhes!["Malte Pilsen"][0]);   // o lote vencido não conta como saldo
            Assert.Equal(antes, Foto(repo));                                    // o vencido não foi tocado
            Assert.Empty(repo.Gravadas);
        }

        [Fact]
        public async Task CT22_pela_API_responde_422_ESTOQUE_INSUFICIENTE()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-VENCIDO", Hoje.AddDays(-1), 100m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 1m));
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));

            var (status, corpo) = await ChamarPelaApi(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal(422, status);
            Assert.Equal("ESTOQUE_INSUFICIENTE", corpo.GetProperty("codigo").GetString());
        }

        [Fact]
        public async Task CT22_lote_vencido_nao_completa_a_falta_do_lote_valido()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-VENCIDO", Hoje.AddDays(-5), 100m));
            repo.Lotes.Add(Lote(2, 1, "M-OK", Hoje.AddDays(60), 5m));          // só 5 kg válidos, precisa de 18
            repo.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(100), 1m));
            repo.Lotes.Add(Lote(4, 3, "Y-1", Hoje.AddDays(100), 1m));
            var antes = Foto(repo);

            var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Contains("disponível 5", ex.Detalhes!["Malte Pilsen"][0]);   // soma só o lote válido
            Assert.Equal(antes, Foto(repo));
        }

        [Fact]
        public async Task CT22_com_lote_vencido_e_lote_valido_suficiente_so_o_valido_e_consumido()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-VENCIDO", Hoje.AddDays(-1), 100m));
            repo.Lotes.Add(Lote(2, 1, "M-OK", Hoje.AddDays(60), 30m));
            repo.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(100), 1m));
            repo.Lotes.Add(Lote(4, 3, "Y-1", Hoje.AddDays(100), 1m));

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            Assert.DoesNotContain(r.Consumos, c => c.CodigoLote == "M-VENCIDO");
            Assert.Equal(100m, repo.Lotes.Single(l => l.Id == 1).QuantidadeAtual);
            Assert.Equal(12m, repo.Lotes.Single(l => l.Id == 2).QuantidadeAtual);
        }

        // ---------- CT-25: falha no meio da produção desfaz tudo ----------

        [Fact]
        public async Task CT25_falha_forcada_depois_de_abater_os_saldos_desfaz_tudo()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 1m));
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));
            var antes = Foto(repo);

            Dictionary<long, (decimal Saldo, long Versao)>? noMomentoDaFalha = null;
            repo.AoAdicionar = () =>
            {
                noMomentoDaFalha = Foto(repo);
                throw new InvalidOperationException("Falha forçada no meio da produção.");
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            // o teste só vale se a baixa realmente tinha acontecido antes da falha
            Assert.NotNull(noMomentoDaFalha);
            Assert.NotEqual(antes, noMomentoDaFalha);
            Assert.True(noMomentoDaFalha![1].Saldo < antes[1].Saldo);

            // rollback: nenhum lote alterado e nenhuma batelada criada
            Assert.Equal(antes, Foto(repo));
            Assert.Empty(repo.Gravadas);
            Assert.Equal(0, repo.Salvamentos);
        }

        [Fact]
        public async Task CT25_falha_ao_gravar_tambem_desfaz_a_baixa_com_o_estoque_dividido_em_dois_lotes()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-CEDO", Hoje.AddDays(30), 10m));
            repo.Lotes.Add(Lote(2, 1, "M-TARDE", Hoje.AddDays(200), 40m));      // a baixa de malte passa pelos dois lotes
            repo.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(100), 1m));
            repo.Lotes.Add(Lote(4, 3, "Y-1", Hoje.AddDays(100), 1m));
            var antes = Foto(repo);
            repo.FalharAoSalvar = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal(antes, Foto(repo));
            Assert.Empty(repo.Gravadas);
        }

        [Fact]
        public async Task CT25_depois_da_falha_o_estoque_continua_utilizavel()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 1m));
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));
            repo.AoAdicionar = () => throw new InvalidOperationException("Falha forçada.");
            await Assert.ThrowsAsync<InvalidOperationException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            repo.AoAdicionar = null;   // a falha passou
            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            Assert.Equal("B-2026-0001", r.Codigo);                                  // a falha não consumiu número de batelada
            Assert.Equal(32m, repo.Lotes.Single(l => l.Id == 1).QuantidadeAtual);    // baixa feita uma única vez (50 - 18)
            Assert.Equal(0.7m, repo.Lotes.Single(l => l.Id == 2).QuantidadeAtual);
            Assert.Single(repo.Gravadas);
        }

        [Fact]
        public async Task CT25_falha_inesperada_vira_500_sem_vazar_detalhes_e_sem_alterar_saldo()
        {
            var (servico, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 1m));
            repo.Lotes.Add(Lote(3, 3, "Y-1", Hoje.AddDays(100), 1m));
            var antes = Foto(repo);
            repo.AoAdicionar = () => throw new InvalidOperationException("segredo interno");

            var (status, corpo) = await ChamarPelaApi(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal(500, status);
            Assert.Equal("ERRO_INTERNO", corpo.GetProperty("codigo").GetString());
            Assert.DoesNotContain("segredo interno", corpo.GetRawText());
            Assert.Equal(antes, Foto(repo));
        }
    }
}