using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-26: resposta da produção com lotes consumidos e estoque antes e depois, e o detalhe com a mesma estrutura.</summary>
    public class ProducaoRespostaTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 24);
        private const long Usuario = 7;

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => ProducaoRespostaTests.Hoje;
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

        private class BateladasFalso : IBateladaRepository
        {
            public List<LoteInsumo> Lotes { get; } = new();
            public List<Batelada> Gravadas { get; } = new();
            private readonly List<Batelada> _pendentes = new();

            public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default) => await operacao();

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

        private static readonly Insumo Malte = new() { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG };
        private static readonly Insumo Lupulo = new() { Id = 2, Nome = "Lúpulo Cascade", UnidadeMedida = UnidadeMedida.KG };

        // IPA: 18 kg de malte e 0,5 kg de lúpulo por 100 L
        private static (BateladaService Servico, BateladasFalso Repo, Receita Receita) Criar()
        {
            var receita = new Receita { Id = 3, Nome = "IPA", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { InsumoId = 1, Insumo = Malte, Quantidade = 18m });
            receita.Itens.Add(new ReceitaItem { InsumoId = 2, Insumo = Lupulo, Quantidade = 0.5m });
            var repo = new BateladasFalso();
            return (new BateladaService(new ReceitasStub { Receita = receita }, repo, new RelogioFixo()), repo, receita);
        }

        private static LoteInsumo Lote(Insumo insumo, long id, string codigo, int validadeEmDias, decimal saldo, decimal preco = 10m) => new()
        {
            Id = id, InsumoId = insumo.Id, Insumo = insumo, CodigoLote = codigo,
            DataEntrada = Hoje.AddDays(-30), DataValidade = Hoje.AddDays(validadeEmDias),
            QuantidadeInicial = saldo, QuantidadeAtual = saldo, PrecoUnitario = preco
        };

        private static BateladaRequest Pedido(decimal litros = 100) => new() { ReceitaId = 3, VolumePlanejado = litros };

        // Malte em dois lotes (10 + 40), um lote vencido que não conta (99) e lúpulo em um lote (5)
        private static (BateladaService Servico, BateladasFalso Repo, Receita Receita) CriarComEstoque()
        {
            var c = Criar();
            c.Repo.Lotes.Add(Lote(Malte, 1, "M-TARDE", 200, 40m));
            c.Repo.Lotes.Add(Lote(Malte, 2, "M-CEDO", 30, 10m));
            c.Repo.Lotes.Add(Lote(Malte, 3, "M-VENCIDO", -5, 99m));
            c.Repo.Lotes.Add(Lote(Lupulo, 4, "L-1", 90, 5m, preco: 100m));
            return c;
        }

        // ---------- A resposta do POST ----------

        [Fact]
        public async Task Resposta_traz_codigo_custo_total_e_o_saldo_de_cada_insumo_antes_e_depois()
        {
            var (servico, _, _) = CriarComEstoque();

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            Assert.Equal("B-2026-0001", r.Codigo);
            Assert.Equal(230m, r.CustoTotal);    // malte 18 x 10 + lúpulo 0,5 x 100

            var insumos = r.Insumos!;
            Assert.Equal(2, insumos.Count);

            var malte = insumos.Single(i => i.InsumoId == 1);
            Assert.Equal((18m, 180m), (malte.Quantidade, malte.Custo));
            Assert.Equal((50m, 32m), (malte.SaldoAntes, malte.SaldoDepois));    // o lote vencido (99) não entra no saldo

            var lupulo = insumos.Single(i => i.InsumoId == 2);
            Assert.Equal((0.5m, 50m), (lupulo.Quantidade, lupulo.Custo));
            Assert.Equal((5m, 4.5m), (lupulo.SaldoAntes, lupulo.SaldoDepois));
        }

        [Fact]
        public async Task Insumo_que_passa_por_dois_lotes_gera_duas_linhas_de_lote_e_uma_linha_de_insumo()
        {
            var (servico, _, _) = CriarComEstoque();

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            var lotesDoMalte = r.Consumos.Where(c => c.InsumoId == 1).ToList();
            Assert.Equal(new[] { ("M-CEDO", 10m, 0m), ("M-TARDE", 8m, 32m) },
                lotesDoMalte.Select(c => (c.CodigoLote, c.Quantidade, c.SaldoAtualLote!.Value)));

            var malte = Assert.Single(r.Insumos!, i => i.InsumoId == 1);
            Assert.Equal(18m, malte.Quantidade);
            Assert.Equal(lotesDoMalte.Sum(c => c.Quantidade), malte.Quantidade);
            Assert.Equal(lotesDoMalte.Sum(c => c.Custo), malte.Custo);
        }

        [Fact]
        public async Task Cada_consumo_traz_insumo_lote_validade_quantidade_custo_e_saldo_atual_do_lote()
        {
            var (servico, _, _) = CriarComEstoque();

            var r = await servico.ProduzirAsync(Pedido(), Usuario);

            var lupulo = Assert.Single(r.Consumos, c => c.InsumoId == 2);
            Assert.Equal(("Lúpulo Cascade", UnidadeMedida.KG, 4L, "L-1", Hoje.AddDays(90)), (lupulo.Insumo, lupulo.UnidadeMedida, lupulo.LoteId, lupulo.CodigoLote, lupulo.Validade));
            Assert.Equal((0.5m, 50m, 4.5m), (lupulo.Quantidade, lupulo.Custo, lupulo.SaldoAtualLote!.Value));
        }

        [Fact]
        public async Task Producao_menor_baixa_menos_e_os_saldos_acompanham()
        {
            var (servico, _, _) = CriarComEstoque();

            var r = await servico.ProduzirAsync(Pedido(50), Usuario);    // metade da receita: 9 kg de malte

            var malte = r.Insumos!.Single(i => i.InsumoId == 1);
            Assert.Equal((9m, 50m, 41m), (malte.Quantidade, malte.SaldoAntes, malte.SaldoDepois));
        }

        // ---------- O detalhe (GET) ----------

        [Fact]
        public async Task Detalhe_devolve_a_mesma_estrutura_de_consumos_da_resposta_do_POST()
        {
            var (servico, repo, receita) = CriarComEstoque();
            var criada = await servico.ProduzirAsync(Pedido(), Usuario);
            repo.Gravadas.Single().Receita = receita;    // o repositório real traz a receita junto

            var detalhe = await servico.ObterAsync(criada.Id);

            Assert.Equal((criada.Id, criada.Codigo, criada.ReceitaId, criada.Receita), (detalhe.Id, detalhe.Codigo, detalhe.ReceitaId, detalhe.Receita));
            Assert.Equal((criada.VolumePlanejado, criada.Status, criada.CustoTotal, criada.DataProducao),
                (detalhe.VolumePlanejado, detalhe.Status, detalhe.CustoTotal, detalhe.DataProducao));
            Assert.Equal(criada.Consumos, detalhe.Consumos);    // mesmos lotes, mesma ordem, mesmos valores
            Assert.Equal(
                criada.Insumos!.Select(i => (i.InsumoId, i.Insumo, i.UnidadeMedida, i.Quantidade, i.Custo)),
                detalhe.Insumos!.Select(i => (i.InsumoId, i.Insumo, i.UnidadeMedida, i.Quantidade, i.Custo)));
        }

        [Fact]
        public async Task Detalhe_nao_traz_o_saldo_antes_e_depois_porque_so_a_resposta_do_POST_tem()
        {
            var (servico, repo, receita) = CriarComEstoque();
            var criada = await servico.ProduzirAsync(Pedido(), Usuario);
            repo.Gravadas.Single().Receita = receita;

            var detalhe = await servico.ObterAsync(criada.Id);

            Assert.All(detalhe.Insumos!, i => { Assert.Null(i.SaldoAntes); Assert.Null(i.SaldoDepois); });
            Assert.All(criada.Insumos!, i => { Assert.NotNull(i.SaldoAntes); Assert.NotNull(i.SaldoDepois); });
        }

        [Fact]
        public async Task Detalhe_mostra_o_saldo_atual_do_lote_mesmo_depois_de_outra_movimentacao()
        {
            var (servico, repo, receita) = CriarComEstoque();
            var criada = await servico.ProduzirAsync(Pedido(), Usuario);
            repo.Gravadas.Single().Receita = receita;
            repo.Lotes.Single(l => l.Id == 1).QuantidadeAtual -= 2m;    // uma perda depois da produção

            var detalhe = await servico.ObterAsync(criada.Id);

            Assert.Equal(30m, detalhe.Consumos.Single(c => c.LoteId == 1).SaldoAtualLote);
        }

        [Fact]
        public async Task Detalhe_de_producao_finalizada_traz_volume_final_rendimento_e_custo_por_litro()
        {
            var (servico, repo, receita) = CriarComEstoque();
            var criada = await servico.ProduzirAsync(Pedido(), Usuario);
            repo.Gravadas.Single().Receita = receita;
            await servico.FinalizarAsync(criada.Id, new FinalizarBateladaRequest { VolumeFinal = 90m });

            var detalhe = await servico.ObterAsync(criada.Id);

            Assert.Equal(StatusBatelada.FINALIZADA, detalhe.Status);
            Assert.Equal((90m, 90m, 2.56m), (detalhe.VolumeFinal, detalhe.RendimentoPercentual, detalhe.CustoPorLitro));   // 230 / 90
        }

        [Fact]
        public async Task Detalhe_de_producao_inexistente_devolve_404_em_portugues()
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterAsync(999));

            Assert.Contains("Produção", ex.Message);
            Assert.DoesNotContain("atelada", ex.Message);
        }
    }
}
