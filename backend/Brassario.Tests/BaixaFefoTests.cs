using System.Text.Json;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Middleware;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-20: baixa na produção pelo lote que vence primeiro (FEFO), LOTE_VENCIDO e consulta dos consumos.</summary>
    public class BaixaFefoTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 24);
        private const long Usuario = 7;

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => BaixaFefoTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
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

        // Repositório em memória: imita a transação (rollback dos saldos se a operação falhar)
        private class BateladasFalso : IBateladaRepository
        {
            public List<LoteInsumo> Lotes { get; } = new();
            public List<Batelada> Gravadas { get; } = new();
            private readonly List<Batelada> _pendentes = new();

            public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default)
            {
                var copia = Lotes.Select(l => (l, l.QuantidadeAtual, l.Versao)).ToList();
                try { return await operacao(); }
                catch
                {
                    foreach (var (lote, qtd, versao) in copia) { lote.QuantidadeAtual = qtd; lote.Versao = versao; }
                    _pendentes.Clear();
                    throw;
                }
            }

            public Task<List<LoteInsumo>> BloquearLotesAsync(IReadOnlyCollection<long> insumoIds, CancellationToken ct = default) =>
                Task.FromResult(Lotes
                    .Where(l => insumoIds.Contains(l.InsumoId) && l.QuantidadeAtual > 0)
                    .OrderBy(l => l.InsumoId).ThenBy(l => l.DataValidade).ThenBy(l => l.DataEntrada).ThenBy(l => l.Id)
                    .ToList());

            public Task<string> ProximoCodigoAsync(int ano, CancellationToken ct = default) =>
                Task.FromResult($"B-{ano}-{Gravadas.Count + 1:0000}");

            public Task AdicionarAsync(Batelada batelada, CancellationToken ct = default) { _pendentes.Add(batelada); return Task.CompletedTask; }

            public Task<Batelada?> ObterComConsumosAsync(long id, CancellationToken ct = default) =>
                Task.FromResult(Gravadas.FirstOrDefault(b => b.Id == id));

            public Task SalvarAsync(CancellationToken ct = default)
            {
                foreach (var b in _pendentes) { b.Id = Gravadas.Count + 1; Gravadas.Add(b); }
                _pendentes.Clear();
                return Task.CompletedTask;
            }
        }

        // Receita simples: 18 kg de Malte Pilsen (insumo 1) por 100 L
        private static (BateladaService Servico, BateladasFalso Repo, Insumo Malte) Criar()
        {
            var malte = new Insumo { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG };
            var receita = new Receita { Id = 3, Nome = "IPA", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { InsumoId = 1, Insumo = malte, Quantidade = 18m });
            var repo = new BateladasFalso();
            return (new BateladaService(new ReceitasStub { Receita = receita }, repo, new RelogioFixo()), repo, malte);
        }

        private static LoteInsumo Lote(Insumo insumo, long id, string codigo, int validadeEmDias, decimal saldo, int entradaHaDias = 30, decimal preco = 10m) => new()
        {
            Id = id, InsumoId = insumo.Id, Insumo = insumo, CodigoLote = codigo,
            DataEntrada = Hoje.AddDays(-entradaHaDias), DataValidade = Hoje.AddDays(validadeEmDias),
            QuantidadeInicial = saldo, QuantidadeAtual = saldo, PrecoUnitario = preco
        };

        private static BateladaRequest Pedido(decimal litros = 100) => new() { ReceitaId = 3, VolumePlanejado = litros };

        private static decimal Saldo(BateladasFalso repo, long id) => repo.Lotes.Single(l => l.Id == id).QuantidadeAtual;

        // Passa a exceção pelo middleware, como na API
        private static async Task<(int Status, JsonElement Corpo)> ChamarPelaApi(Func<Task> acao)
        {
            var contexto = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
            contexto.Response.Body = new MemoryStream();
            await new ExceptionHandlingMiddleware(_ => acao(), NullLogger<ExceptionHandlingMiddleware>.Instance).InvokeAsync(contexto);
            contexto.Response.Body.Position = 0;
            return (contexto.Response.StatusCode, JsonDocument.Parse(await new StreamReader(contexto.Response.Body).ReadToEndAsync()).RootElement);
        }

        // ---------- Um lote cobre tudo ----------

        [Fact]
        public async Task Um_lote_cobre_tudo_e_gera_um_consumo_uma_movimentacao_e_uma_versao()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-1", 60, 50m));

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            var consumo = Assert.Single(r.Consumos);
            Assert.Equal(("M-1", 18m), (consumo.CodigoLote, consumo.Quantidade));
            Assert.Equal(32m, Saldo(repo, 1));
            Assert.Equal(1, repo.Lotes[0].Versao);

            var batelada = Assert.Single(repo.Gravadas);
            var mov = Assert.Single(batelada.Movimentacoes);
            Assert.Equal(TipoMovimentacao.SAIDA_PRODUCAO, mov.Tipo);
            Assert.Equal(-18m, mov.Quantidade);
            Assert.Same(repo.Lotes[0], mov.LoteInsumo);
            Assert.Equal(Usuario, mov.UsuarioId);
            Assert.Equal(new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc), mov.DataHora);
        }

        // ---------- Dois lotes: o primeiro esgota ----------

        [Fact]
        public async Task Dois_lotes_o_que_vence_primeiro_esgota_e_o_restante_sai_do_seguinte()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-TARDE", 200, 40m));
            repo.Lotes.Add(Lote(malte, 2, "M-CEDO", 30, 10m));

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            Assert.Equal(new[] { ("M-CEDO", 10m), ("M-TARDE", 8m) }, r.Consumos.Select(c => (c.CodigoLote, c.Quantidade)));
            Assert.Equal(0m, Saldo(repo, 2));
            Assert.Equal(32m, Saldo(repo, 1));
            Assert.Equal(1, repo.Lotes.Single(l => l.Id == 2).Versao);
            Assert.Equal(1, repo.Lotes.Single(l => l.Id == 1).Versao);

            var movs = repo.Gravadas.Single().Movimentacoes;
            Assert.Equal(2, movs.Count);                                   // uma linha por lote tocado
            Assert.Equal(-18m, movs.Sum(m => m.Quantidade));
        }

        [Fact]
        public async Task Empate_de_validade_consome_primeiro_o_lote_que_entrou_antes()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-NOVO", 30, 100m, entradaHaDias: 2));
            repo.Lotes.Add(Lote(malte, 2, "M-VELHO", 30, 100m, entradaHaDias: 20));

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            Assert.Equal("M-VELHO", Assert.Single(r.Consumos).CodigoLote);
            Assert.Equal(82m, Saldo(repo, 2));
            Assert.Equal(100m, Saldo(repo, 1));
        }

        [Fact]
        public async Task Lote_que_vence_hoje_ainda_e_consumido()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-HOJE", 0, 100m));
            repo.Lotes.Add(Lote(malte, 2, "M-LONGE", 90, 100m));

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            Assert.Equal("M-HOJE", Assert.Single(r.Consumos).CodigoLote);
        }

        // ---------- Lote vencido ----------

        [Fact]
        public async Task Lote_vencido_e_ignorado_mesmo_vencendo_antes_dos_outros()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-VENCIDO", -1, 100m));
            repo.Lotes.Add(Lote(malte, 2, "M-OK", 60, 30m));

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            Assert.Equal("M-OK", Assert.Single(r.Consumos).CodigoLote);
            Assert.Equal(100m, Saldo(repo, 1));
            Assert.Equal(0, repo.Lotes.Single(l => l.Id == 1).Versao);
        }

        [Fact]
        public async Task So_lote_vencido_devolve_422_LOTE_VENCIDO_sem_alterar_nada()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-VENCIDO", -3, 100m));

            var (status, corpo) = await ChamarPelaApi(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal(422, status);
            Assert.Equal("LOTE_VENCIDO", corpo.GetProperty("codigo").GetString());
            Assert.Contains("M-VENCIDO", corpo.GetProperty("detalhes").GetProperty("Malte Pilsen")[0].GetString());
            Assert.Equal(100m, Saldo(repo, 1));
            Assert.Empty(repo.Gravadas);
        }

        [Fact]
        public async Task Lote_vencido_e_lote_valido_que_nao_basta_devolve_ESTOQUE_INSUFICIENTE()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-VENCIDO", -3, 100m));
            repo.Lotes.Add(Lote(malte, 2, "M-OK", 60, 5m));   // só 5 válidos, precisa de 18

            var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Contains("disponível 5", ex.Detalhes!["Malte Pilsen"][0]);
            Assert.Contains("falta 13", ex.Detalhes["Malte Pilsen"][0]);
        }

        [Fact]
        public async Task Um_insumo_so_vencido_e_outro_sem_saldo_devolve_ESTOQUE_INSUFICIENTE_listando_os_dois()
        {
            var malte = new Insumo { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG };
            var lupulo = new Insumo { Id = 2, Nome = "Lúpulo Cascade", UnidadeMedida = UnidadeMedida.KG };
            var receita = new Receita { Id = 3, Nome = "IPA", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { InsumoId = 1, Insumo = malte, Quantidade = 18m });
            receita.Itens.Add(new ReceitaItem { InsumoId = 2, Insumo = lupulo, Quantidade = 0.3m });
            var repo = new BateladasFalso();
            var servico = new BateladaService(new ReceitasStub { Receita = receita }, repo, new RelogioFixo());
            repo.Lotes.Add(Lote(malte, 1, "M-VENCIDO", -3, 100m));
            repo.Lotes.Add(Lote(lupulo, 2, "L-1", 90, 0.1m));   // falta lúpulo

            var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal(2, ex.Detalhes!.Count);
        }

        // ---------- Saldo insuficiente ----------

        [Fact]
        public async Task Saldo_insuficiente_informa_o_insumo_e_quanto_falta_e_nada_e_descontado()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-1", 30, 10m));
            repo.Lotes.Add(Lote(malte, 2, "M-2", 90, 3m));   // total 13, precisa de 18

            var (status, corpo) = await ChamarPelaApi(() => servico.ProduzirAsync(Pedido(), Usuario));

            Assert.Equal(422, status);
            Assert.Equal("ESTOQUE_INSUFICIENTE", corpo.GetProperty("codigo").GetString());
            Assert.Contains("falta 5", corpo.GetProperty("detalhes").GetProperty("Malte Pilsen")[0].GetString());
            Assert.Equal(10m, Saldo(repo, 1));
            Assert.Equal(3m, Saldo(repo, 2));
            Assert.All(repo.Lotes, l => Assert.Equal(0, l.Versao));
            Assert.Empty(repo.Gravadas);
        }

        // ---------- GET /bateladas/{id}/consumos ----------

        [Fact]
        public async Task Consumos_da_producao_trazem_insumo_codigo_do_lote_quantidade_e_validade()
        {
            var (servico, repo, malte) = Criar();
            repo.Lotes.Add(Lote(malte, 1, "M-TARDE", 200, 40m));
            repo.Lotes.Add(Lote(malte, 2, "M-CEDO", 30, 10m));
            var producao = await servico.ProduzirAsync(Pedido(), Usuario);

            var consumos = await servico.ObterConsumosAsync(producao.Id);

            Assert.Equal(2, consumos.Count);
            var primeiro = consumos[0];
            Assert.Equal("Malte Pilsen", primeiro.Insumo);
            Assert.Equal("M-CEDO", primeiro.CodigoLote);
            Assert.Equal(10m, primeiro.Quantidade);
            Assert.Equal(Hoje.AddDays(30), primeiro.Validade);
            Assert.Equal(("M-TARDE", 8m), (consumos[1].CodigoLote, consumos[1].Quantidade));
        }

        [Fact]
        public async Task Consumos_de_producao_inexistente_vira_404()
        {
            var (servico, _, _) = Criar();
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterConsumosAsync(999));
        }

        // ---------- Concorrência otimista (coluna Versao) ----------

        [Fact]
        public async Task Conflito_de_versao_do_lote_vira_409_CONFLITO_CONCORRENCIA()
        {
            var (status, corpo) = await ChamarPelaApi(() => throw new DbUpdateConcurrencyException("Versao mudou"));

            Assert.Equal(409, status);
            Assert.Equal("CONFLITO_CONCORRENCIA", corpo.GetProperty("codigo").GetString());
        }
    }
}
