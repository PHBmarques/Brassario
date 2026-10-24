using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-27: finalizar e cancelar produção, estorno nos lotes de origem, detalhe da cancelada e custo zerado na lista.</summary>
    public class CancelamentoProducaoTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 25);
        private static readonly DateTime Agora = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
        private const long UsuarioLogado = 7;

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => CancelamentoProducaoTests.Hoje;
            public DateTime AgoraUtc => Agora;
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
                foreach (var b in _pendentes)
                {
                    b.Id = Gravadas.Count + 1;
                    // O EF preenche as chaves estrangeiras ao gravar; o falso faz o mesmo
                    foreach (var consumo in b.Consumos) consumo.LoteInsumoId = consumo.LoteInsumo.Id;
                    Gravadas.Add(b);
                }
                _pendentes.Clear();
                return Task.CompletedTask;
            }
        }

        private static readonly Insumo Malte = new() { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG };
        private static readonly Insumo Lupulo = new() { Id = 2, Nome = "Lúpulo Cascade", UnidadeMedida = UnidadeMedida.KG };

        private static LoteInsumo Lote(Insumo insumo, long id, string codigo, int validadeEmDias, decimal saldo, decimal preco = 10m) => new()
        {
            Id = id, InsumoId = insumo.Id, Insumo = insumo, CodigoLote = codigo,
            DataEntrada = Hoje.AddDays(-30), DataValidade = Hoje.AddDays(validadeEmDias),
            QuantidadeInicial = saldo, QuantidadeAtual = saldo, PrecoUnitario = preco
        };

        private sealed record Cenario(BateladaService Servico, BateladasFalso Repo, BateladaResponse Producao);

        // Produz 100 L de IPA: o malte (18 kg) sai de DOIS lotes (10 + 8) e o lúpulo (0,5 kg) de um. Custo total 230.
        private static async Task<Cenario> ProduzirAsync()
        {
            var receita = new Receita { Id = 3, Nome = "IPA", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { InsumoId = 1, Insumo = Malte, Quantidade = 18m });
            receita.Itens.Add(new ReceitaItem { InsumoId = 2, Insumo = Lupulo, Quantidade = 0.5m });

            var repo = new BateladasFalso();
            repo.Lotes.Add(Lote(Malte, 1, "M-TARDE", 200, 40m));
            repo.Lotes.Add(Lote(Malte, 2, "M-CEDO", 30, 10m));
            repo.Lotes.Add(Lote(Lupulo, 4, "L-1", 90, 5m, preco: 100m));

            var servico = new BateladaService(new ReceitasStub { Receita = receita }, repo, new RelogioFixo());
            var producao = await servico.ProduzirAsync(new BateladaRequest { ReceitaId = 3, VolumePlanejado = 100 }, UsuarioLogado);
            repo.Gravadas.Single().Receita = receita;    // o repositório real traz a receita junto
            return new Cenario(servico, repo, producao);
        }

        private static decimal Saldo(BateladasFalso repo, long id) => repo.Lotes.Single(l => l.Id == id).QuantidadeAtual;

        private static CancelarBateladaRequest Motivo(string texto) => new() { Motivo = texto };

        // ---------- Finalizar ----------

        [Fact]
        public async Task Finalizar_devolve_rendimento_e_custo_por_litro()
        {
            var c = await ProduzirAsync();

            var r = await c.Servico.FinalizarAsync(c.Producao.Id, new FinalizarBateladaRequest { VolumeFinal = 90m });

            Assert.Equal((StatusBatelada.FINALIZADA, 90m, 90m, 2.56m), (r.Status, r.VolumeFinal, r.RendimentoPercentual, r.CustoPorLitro));   // 230 / 90
            Assert.Equal(230m, r.CustoTotal);
        }

        [Theory]
        [InlineData(200.0, 200.0, 1.15)]    // exatamente 2 vezes o planejado: aceito, mesmo com rendimento acima de 130%
        [InlineData(50.0, 50.0, 4.6)]       // rendimento baixo: o aviso é só da tela, o backend aceita
        public async Task Finalizar_aceita_qualquer_volume_ate_duas_vezes_o_planejado(double volume, double rendimento, double custoPorLitro)
        {
            var c = await ProduzirAsync();

            var r = await c.Servico.FinalizarAsync(c.Producao.Id, new FinalizarBateladaRequest { VolumeFinal = (decimal)volume });

            Assert.Equal(((decimal)rendimento, (decimal)custoPorLitro), (r.RendimentoPercentual, r.CustoPorLitro));
        }

        [Theory]
        [InlineData(200.01)]
        [InlineData(0.0)]
        [InlineData(-5.0)]
        public async Task Finalizar_com_volume_fora_do_limite_gera_400_e_a_producao_continua_em_andamento(double volume)
        {
            var c = await ProduzirAsync();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                c.Servico.FinalizarAsync(c.Producao.Id, new FinalizarBateladaRequest { VolumeFinal = (decimal)volume }));

            Assert.True(ex.Detalhes.ContainsKey("volumeFinal"));
            Assert.Equal(StatusBatelada.EM_ANDAMENTO, c.Repo.Gravadas.Single().Status);
        }

        // ---------- Cancelar ----------

        [Fact]
        public async Task Cancelar_devolve_a_quantidade_a_cada_lote_de_origem_inclusive_o_insumo_de_dois_lotes()
        {
            var c = await ProduzirAsync();
            Assert.Equal((0m, 32m, 4.5m), (Saldo(c.Repo, 2), Saldo(c.Repo, 1), Saldo(c.Repo, 4)));

            var r = await c.Servico.CancelarAsync(c.Producao.Id, Motivo("  Lote contaminado  "), UsuarioLogado);

            Assert.Equal((10m, 40m, 5m), (Saldo(c.Repo, 2), Saldo(c.Repo, 1), Saldo(c.Repo, 4)));

            var estornos = c.Repo.Gravadas.Single().Movimentacoes.Where(m => m.Tipo == TipoMovimentacao.ESTORNO).ToList();
            Assert.Equal(3, estornos.Count);                                           // uma movimentação por lote
            Assert.All(estornos, m =>
            {
                Assert.True(m.Quantidade > 0);
                Assert.Equal("Lote contaminado", m.Motivo);
                Assert.Equal((UsuarioLogado, Agora), (m.UsuarioId, m.DataHora));
            });
            Assert.Equal(new[] { 10m, 8m, 0.5m }, estornos.Select(m => m.Quantidade));
        }

        [Fact]
        public async Task Cancelar_grava_motivo_e_data_e_responde_com_a_producao_cancelada_e_os_estornos()
        {
            var c = await ProduzirAsync();

            var r = await c.Servico.CancelarAsync(c.Producao.Id, Motivo("Lote contaminado"), UsuarioLogado);

            var gravada = c.Repo.Gravadas.Single();
            Assert.Equal((StatusBatelada.CANCELADA, "Lote contaminado", Agora), (gravada.Status, gravada.MotivoCancelamento, gravada.CanceladoEm));

            Assert.Equal((StatusBatelada.CANCELADA, "Lote contaminado", Agora), (r.Status, r.MotivoCancelamento, r.CanceladoEm));
            Assert.Equal(0m, r.CustoTotal);
            Assert.Equal(
                new[] { ("Malte Pilsen", "M-CEDO", 10m), ("Malte Pilsen", "M-TARDE", 8m), ("Lúpulo Cascade", "L-1", 0.5m) },
                r.Estornos!.Select(e => (e.Insumo, e.CodigoLote, e.Quantidade)));
        }

        [Fact]
        public async Task Cancelar_duas_vezes_gera_422_STATUS_INVALIDO_sem_estornar_de_novo()
        {
            var c = await ProduzirAsync();
            await c.Servico.CancelarAsync(c.Producao.Id, Motivo("Primeiro cancelamento"), UsuarioLogado);

            var ex = await Assert.ThrowsAsync<NegocioException>(() => c.Servico.CancelarAsync(c.Producao.Id, Motivo("Segundo cancelamento"), UsuarioLogado));

            Assert.Equal("STATUS_INVALIDO", ex.Codigo);
            Assert.Equal((10m, 40m, 5m), (Saldo(c.Repo, 2), Saldo(c.Repo, 1), Saldo(c.Repo, 4)));    // o estoque não passou do original
            var gravada = c.Repo.Gravadas.Single();
            Assert.Equal(3, gravada.Movimentacoes.Count(m => m.Tipo == TipoMovimentacao.ESTORNO));
            Assert.Equal("Primeiro cancelamento", gravada.MotivoCancelamento);
        }

        [Fact]
        public async Task Finalizar_producao_cancelada_e_cancelar_producao_finalizada_geram_422()
        {
            var cancelada = await ProduzirAsync();
            await cancelada.Servico.CancelarAsync(cancelada.Producao.Id, Motivo("Cancelada de propósito"), UsuarioLogado);
            var finalizada = await ProduzirAsync();
            await finalizada.Servico.FinalizarAsync(finalizada.Producao.Id, new FinalizarBateladaRequest { VolumeFinal = 95m });

            var e1 = await Assert.ThrowsAsync<NegocioException>(() =>
                cancelada.Servico.FinalizarAsync(cancelada.Producao.Id, new FinalizarBateladaRequest { VolumeFinal = 95m }));
            var e2 = await Assert.ThrowsAsync<NegocioException>(() =>
                finalizada.Servico.CancelarAsync(finalizada.Producao.Id, Motivo("Tarde demais"), UsuarioLogado));

            Assert.Equal(("STATUS_INVALIDO", "STATUS_INVALIDO"), (e1.Codigo, e2.Codigo));
            Assert.Equal((0m, 32m, 4.5m), (Saldo(finalizada.Repo, 2), Saldo(finalizada.Repo, 1), Saldo(finalizada.Repo, 4)));   // finalizada não devolve nada
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        [InlineData("abc")]
        [InlineData(" 1234 ")]
        public async Task Motivo_com_menos_de_5_caracteres_gera_400_e_nada_muda(string motivo)
        {
            var c = await ProduzirAsync();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Servico.CancelarAsync(c.Producao.Id, Motivo(motivo), UsuarioLogado));

            Assert.True(ex.Detalhes.ContainsKey("motivo"));
            var gravada = c.Repo.Gravadas.Single();
            Assert.Equal(StatusBatelada.EM_ANDAMENTO, gravada.Status);
            Assert.Null(gravada.MotivoCancelamento);
            Assert.Null(gravada.CanceladoEm);
            Assert.Equal((0m, 32m), (Saldo(c.Repo, 2), Saldo(c.Repo, 1)));
        }

        // ---------- Detalhe da produção cancelada ----------

        [Fact]
        public async Task Detalhe_da_producao_cancelada_traz_motivo_data_estornos_e_custo_zero()
        {
            var c = await ProduzirAsync();
            await c.Servico.CancelarAsync(c.Producao.Id, Motivo("Lote contaminado"), UsuarioLogado);

            var d = await c.Servico.ObterAsync(c.Producao.Id);

            Assert.Equal((StatusBatelada.CANCELADA, "Lote contaminado", Agora, 0m), (d.Status, d.MotivoCancelamento, d.CanceladoEm, d.CustoTotal));
            Assert.Equal(3, d.Estornos!.Count);
            Assert.Equal(("Malte Pilsen", "M-CEDO", 10m), (d.Estornos[0].Insumo, d.Estornos[0].CodigoLote, d.Estornos[0].Quantidade));
            Assert.Null(d.RendimentoPercentual);
            Assert.Null(d.CustoPorLitro);
        }

        [Fact]
        public async Task Detalhe_da_producao_em_andamento_nao_traz_motivo_data_nem_estornos()
        {
            var c = await ProduzirAsync();

            var d = await c.Servico.ObterAsync(c.Producao.Id);

            Assert.Equal(StatusBatelada.EM_ANDAMENTO, d.Status);
            Assert.Null(d.MotivoCancelamento);
            Assert.Null(d.CanceladoEm);
            Assert.Null(d.Estornos);
            Assert.Equal(230m, d.CustoTotal);
        }

        // ---------- Lista: cancelada com custo 0,00 ----------

        private class HistoricoFalso : IHistoricoRepository
        {
            public List<Batelada> Bateladas { get; } = new();

            public Task<(List<Batelada> Itens, int Total)> ListarBateladasAsync(FiltroBateladas filtro, int pagina, int tamanho, CancellationToken ct = default) =>
                Task.FromResult((Bateladas, Bateladas.Count));

            public Task<(List<MovimentacaoEstoque> Itens, int Total)> ListarMovimentacoesAsync(FiltroMovimentacoes filtro, int pagina, int tamanho, CancellationToken ct = default) =>
                throw new NotSupportedException();
        }

        [Fact]
        public async Task Na_lista_a_producao_cancelada_aparece_com_custo_zero_e_as_outras_mantem_o_custo()
        {
            var receita = new Receita { Id = 3, Nome = "IPA" };
            var usuario = new Usuario { Id = UsuarioLogado, Nome = "Ana" };
            Batelada Producao(long id, StatusBatelada status) => new()
            {
                Id = id, Codigo = $"B-2026-{id:0000}", ReceitaId = 3, Receita = receita, UsuarioId = UsuarioLogado, Usuario = usuario,
                VolumePlanejado = 100m, Status = status, CustoTotal = 230m, DataProducao = Agora
            };
            var repo = new HistoricoFalso();
            repo.Bateladas.AddRange(new[] { Producao(1, StatusBatelada.CANCELADA), Producao(2, StatusBatelada.FINALIZADA), Producao(3, StatusBatelada.EM_ANDAMENTO) });

            var pagina = await new HistoricoService(repo).ListarBateladasAsync(null, null, null, null, null, null);

            Assert.Equal(new[] { 0m, 230m, 230m }, pagina.Itens.Select(i => i.CustoTotal));
            Assert.Equal(StatusBatelada.CANCELADA, pagina.Itens[0].Status);
        }
    }
}
