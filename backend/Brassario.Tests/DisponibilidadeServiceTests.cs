using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    public class DisponibilidadeServiceTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 20);

        // Relógio fixo: "hoje" é sempre 20/10/2026
        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => DisponibilidadeServiceTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        }

        // Receita simulada: o serviço só usa ObterParaProducaoAsync
        private class ReceitasStub : IReceitaService
        {
            public Receita? Receita { get; set; }
            public Exception? Erro { get; set; }

            public Task<Receita> ObterParaProducaoAsync(long id, CancellationToken ct = default)
            {
                if (Erro is not null) throw Erro;
                return Task.FromResult(Receita!);
            }

            public Task<List<ReceitaResumoResponse>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default) =>
                throw new NotSupportedException();
            public Task<ReceitaResponse> ObterAsync(long id, CancellationToken ct = default) =>
                throw new NotSupportedException();
            public Task<ReceitaResponse> CriarAsync(ReceitaRequest request, CancellationToken ct = default) =>
                throw new NotSupportedException();
            public Task<ReceitaResponse> AtualizarAsync(long id, ReceitaRequest request, CancellationToken ct = default) =>
                throw new NotSupportedException();
            public Task<ReceitaResponse> AlterarStatusAsync(long id, bool ativa, CancellationToken ct = default) =>
                throw new NotSupportedException();
        }

        // Repositório de lotes em memória; conta as gravações para provar que a simulação não grava nada
        private class LotesFalso : ILoteRepository
        {
            public List<LoteInsumo> Lotes { get; } = new();
            public int Salvamentos { get; private set; }
            public int Adicoes { get; private set; }

            public Task<bool> ExisteCodigoAsync(long insumoId, string codigoLote, CancellationToken ct = default) =>
                Task.FromResult(Lotes.Any(l => l.InsumoId == insumoId &&
                    string.Equals(l.CodigoLote, codigoLote.Trim(), StringComparison.OrdinalIgnoreCase)));

            public Task<int> ProximoSequencialAsync(long insumoId, string prefixo, CancellationToken ct = default) => Task.FromResult(1);

            public Task<List<LoteInsumo>> ListarPorInsumosAsync(
                IReadOnlyCollection<long> insumoIds, bool incluirEsgotados, CancellationToken ct = default)
            {
                var q = Lotes.Where(l => insumoIds.Contains(l.InsumoId));
                if (!incluirEsgotados) q = q.Where(l => l.QuantidadeAtual > 0);
                return Task.FromResult(q.ToList());
            }

            public Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default)
            {
                Adicoes++;
                Lotes.Add(lote);
                return Task.CompletedTask;
            }

            public Task SalvarAsync(CancellationToken ct = default)
            {
                Salvamentos++;
                return Task.CompletedTask;
            }
        }

        private static (DisponibilidadeService Servico, ReceitasStub Receitas, LotesFalso Lotes) Criar(bool receitaAtiva = true)
        {
            var receitas = new ReceitasStub { Receita = NovaReceita(receitaAtiva) };
            var lotes = new LotesFalso();
            return (new DisponibilidadeService(receitas, lotes, new RelogioFixo()), receitas, lotes);
        }

        // IPA Cascade: base de 100 L, 18 kg de Malte Pilsen (insumo 1) e 0,3 kg de Lúpulo Cascade (insumo 2)
        private static Receita NovaReceita(bool ativa = true)
        {
            var malte = new Insumo { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG };
            var lupulo = new Insumo { Id = 2, Nome = "Lúpulo Cascade", UnidadeMedida = UnidadeMedida.KG };
            var receita = new Receita { Id = 3, Nome = "IPA Cascade", VolumeBaseLitros = 100, Ativa = ativa };
            receita.Itens.Add(new ReceitaItem { InsumoId = 1, Insumo = malte, Quantidade = 18m });
            receita.Itens.Add(new ReceitaItem { InsumoId = 2, Insumo = lupulo, Quantidade = 0.3m });
            return receita;
        }

        private static LoteInsumo Lote(long id, long insumoId, string codigo, DateOnly validade, decimal saldo, DateOnly? entrada = null) => new()
        {
            Id = id,
            InsumoId = insumoId,
            CodigoLote = codigo,
            DataEntrada = entrada ?? Hoje.AddDays(-30),
            DataValidade = validade,
            QuantidadeInicial = saldo,
            QuantidadeAtual = saldo
        };

        private static DisponibilidadeItemResponse Item(DisponibilidadeResponse r, long insumoId) =>
            r.Itens.Single(i => i.InsumoId == insumoId);

        // ---------- Escala (RN-04) ----------

        [Fact]
        public async Task Escala_as_quantidades_pelo_volume_pedido()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));
            lotes.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 5m));

            var metade = await servico.SimularAsync(3, 50);   // metade do volume base
            var dobro = await servico.SimularAsync(3, 200);   // o dobro

            Assert.Equal(9m, Item(metade, 1).Necessario);
            Assert.Equal(0.15m, Item(metade, 2).Necessario);
            Assert.Equal(36m, Item(dobro, 1).Necessario);
            Assert.Equal(0.6m, Item(dobro, 2).Necessario);
        }

        [Fact]
        public void Calculo_escala_e_arredonda_para_tres_casas()
        {
            Assert.Equal(9m, CalculoProducao.Escalar(18m, 100m, 50m));
            Assert.Equal(0.099m, CalculoProducao.Escalar(0.3m, 100m, 33m));
            Assert.Equal(0.333m, CalculoProducao.Escalar(1m, 3m, 1m));
        }

        // ---------- Disponibilidade e FEFO ----------

        [Fact]
        public async Task Com_estoque_suficiente_pode_produzir_e_mostra_o_lote_previsto()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(10, 1, "M-1", Hoje.AddDays(100), 50m));
            lotes.Lotes.Add(Lote(20, 2, "L-1", Hoje.AddDays(60), 1m));

            var r = await servico.SimularAsync(3, 100);

            Assert.True(r.PodeProduzir);
            Assert.Equal(3, r.ReceitaId);
            Assert.Equal(100m, r.Volume);

            var malte = Item(r, 1);
            Assert.True(malte.Suficiente);
            Assert.Equal(18m, malte.Necessario);
            Assert.Equal(50m, malte.Disponivel);
            Assert.Equal(0m, malte.Falta);
            var previsto = Assert.Single(malte.LotesPrevistos);
            Assert.Equal(10, previsto.LoteId);
            Assert.Equal("M-1", previsto.CodigoLote);
            Assert.Equal(18m, previsto.Quantidade);
        }

        [Fact]
        public async Task Consumo_divide_entre_lotes_comecando_pelo_que_vence_primeiro()
        {
            var (servico, _, lotes) = Criar();
            // O lote que vence depois foi cadastrado primeiro: a ordem tem que ser pela validade, não pelo cadastro
            lotes.Lotes.Add(Lote(1, 1, "M-TARDE", Hoje.AddDays(200), 20m));
            lotes.Lotes.Add(Lote(2, 1, "M-CEDO", Hoje.AddDays(30), 10m));
            lotes.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(90), 1m));

            var r = await servico.SimularAsync(3, 100);   // malte: necessário 18

            var previstos = Item(r, 1).LotesPrevistos;
            Assert.Equal(2, previstos.Count);
            Assert.Equal("M-CEDO", previstos[0].CodigoLote);
            Assert.Equal(10m, previstos[0].Quantidade);   // esvazia o que vence primeiro
            Assert.Equal("M-TARDE", previstos[1].CodigoLote);
            Assert.Equal(8m, previstos[1].Quantidade);    // e tira o resto do próximo
            Assert.True(r.PodeProduzir);
        }

        [Fact]
        public async Task Empate_de_validade_usa_quem_entrou_primeiro()
        {
            var (servico, _, lotes) = Criar();
            var validade = Hoje.AddDays(40);
            lotes.Lotes.Add(Lote(1, 1, "M-NOVO", validade, 30m, entrada: Hoje.AddDays(-2)));
            lotes.Lotes.Add(Lote(2, 1, "M-ANTIGO", validade, 30m, entrada: Hoje.AddDays(-20)));
            lotes.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(90), 1m));

            var r = await servico.SimularAsync(3, 100);

            Assert.Equal("M-ANTIGO", Item(r, 1).LotesPrevistos[0].CodigoLote);
        }

        [Fact]
        public async Task Estoque_insuficiente_mostra_a_falta_e_o_que_existe()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(10, 1, "M-1", Hoje.AddDays(100), 50m));
            lotes.Lotes.Add(Lote(31, 2, "NF1234-A", Hoje.AddDays(160), 0.12m));   // precisa de 0,3

            var r = await servico.SimularAsync(3, 100);

            Assert.False(r.PodeProduzir);
            Assert.True(Item(r, 1).Suficiente);

            var lupulo = Item(r, 2);
            Assert.False(lupulo.Suficiente);
            Assert.Equal(0.3m, lupulo.Necessario);
            Assert.Equal(0.12m, lupulo.Disponivel);
            Assert.Equal(0.18m, lupulo.Falta);
            var previsto = Assert.Single(lupulo.LotesPrevistos);
            Assert.Equal(0.12m, previsto.Quantidade);   // tudo o que existe
        }

        [Fact]
        public async Task Insumo_sem_nenhum_lote_falta_por_inteiro()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(10, 1, "M-1", Hoje.AddDays(100), 50m));   // lúpulo sem lote

            var r = await servico.SimularAsync(3, 100);

            var lupulo = Item(r, 2);
            Assert.False(r.PodeProduzir);
            Assert.Equal(0m, lupulo.Disponivel);
            Assert.Equal(0.3m, lupulo.Falta);
            Assert.Empty(lupulo.LotesPrevistos);
        }

        // ---------- Lotes que não entram (RN-03) ----------

        [Fact]
        public async Task Lote_vencido_e_ignorado_no_saldo_e_nos_lotes_previstos()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(1, 1, "M-VENCIDO", Hoje.AddDays(-1), 100m));   // vencido ontem
            lotes.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(90), 1m));

            var r = await servico.SimularAsync(3, 100);

            var malte = Item(r, 1);
            Assert.False(r.PodeProduzir);
            Assert.Equal(0m, malte.Disponivel);
            Assert.Equal(18m, malte.Falta);
            Assert.Empty(malte.LotesPrevistos);
        }

        [Fact]
        public async Task Lote_vencido_nao_entra_mesmo_quando_ha_outro_valido()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(1, 1, "M-VENCIDO", Hoje.AddDays(-5), 100m));
            lotes.Lotes.Add(Lote(2, 1, "M-OK", Hoje.AddDays(60), 20m));
            lotes.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(90), 1m));

            var r = await servico.SimularAsync(3, 100);

            var malte = Item(r, 1);
            Assert.Equal(20m, malte.Disponivel);   // só o lote válido
            Assert.All(malte.LotesPrevistos, p => Assert.Equal("M-OK", p.CodigoLote));
            Assert.True(r.PodeProduzir);
        }

        [Fact]
        public async Task Lote_que_vence_hoje_ainda_pode_ser_usado()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(1, 1, "M-HOJE", Hoje, 30m));
            lotes.Lotes.Add(Lote(2, 2, "L-1", Hoje, 1m));

            var r = await servico.SimularAsync(3, 100);

            Assert.True(r.PodeProduzir);
            Assert.Equal("M-HOJE", Item(r, 1).LotesPrevistos[0].CodigoLote);
        }

        [Fact]
        public async Task Lote_sem_saldo_e_ignorado()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(1, 1, "M-ZERADO", Hoje.AddDays(10), 0m));
            lotes.Lotes.Add(Lote(2, 1, "M-OK", Hoje.AddDays(50), 20m));
            lotes.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(90), 1m));

            var r = await servico.SimularAsync(3, 100);

            Assert.DoesNotContain(Item(r, 1).LotesPrevistos, p => p.CodigoLote == "M-ZERADO");
            Assert.Equal(20m, Item(r, 1).Disponivel);
        }

        // ---------- Não altera nada ----------

        [Fact]
        public async Task Simulacao_nao_altera_saldos_nem_grava_nada()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(30), 10m));
            lotes.Lotes.Add(Lote(2, 1, "M-2", Hoje.AddDays(90), 20m));
            lotes.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(90), 0.1m));
            var antes = lotes.Lotes.Select(l => (l.Id, l.QuantidadeAtual, l.Versao)).ToList();

            await servico.SimularAsync(3, 100);
            await servico.SimularAsync(3, 100);   // repetir dá o mesmo resultado, nada foi consumido

            var depois = lotes.Lotes.Select(l => (l.Id, l.QuantidadeAtual, l.Versao)).ToList();
            Assert.Equal(antes, depois);
            Assert.Equal(0, lotes.Salvamentos);
            Assert.Equal(0, lotes.Adicoes);
        }

        // ---------- Itens ----------

        [Fact]
        public async Task Itens_voltam_em_ordem_de_nome_com_a_unidade_do_insumo()
        {
            var (servico, _, lotes) = Criar();
            lotes.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m));
            lotes.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 5m));

            var r = await servico.SimularAsync(3, 100);

            Assert.Equal(new[] { "Lúpulo Cascade", "Malte Pilsen" }, r.Itens.Select(i => i.Insumo).ToArray());
            Assert.All(r.Itens, i => Assert.Equal(UnidadeMedida.KG, i.UnidadeMedida));
            Assert.Equal("IPA Cascade", r.Receita);
        }

        // ---------- Erros ----------

        [Theory]
        [InlineData(null)]
        [InlineData(0.0)]
        [InlineData(-10.0)]
        [InlineData(0.001)]
        public async Task Volume_ausente_zero_negativo_ou_menor_que_um_centilitro_gera_400(double? volume)
        {
            var (servico, _, _) = Criar();
            decimal? pedido = volume.HasValue ? (decimal)volume.Value : null;

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.SimularAsync(3, pedido));
            Assert.True(ex.Detalhes.ContainsKey("volume"));
        }

        [Fact]
        public async Task Volume_e_validado_antes_de_procurar_a_receita()
        {
            var (servico, receitas, _) = Criar();
            receitas.Erro = new RecursoNaoEncontradoException("Receita", 3);

            await Assert.ThrowsAsync<ValidacaoException>(() => servico.SimularAsync(3, 0));
        }

        [Fact]
        public async Task Receita_inexistente_gera_404()
        {
            var (servico, receitas, _) = Criar();
            receitas.Erro = new RecursoNaoEncontradoException("Receita", 99);

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.SimularAsync(99, 100));
        }

        [Fact]
        public async Task Receita_inativa_gera_422_status_invalido()
        {
            var (servico, receitas, _) = Criar();
            receitas.Erro = new NegocioException("A receita está inativa e não pode ser produzida.", "STATUS_INVALIDO");

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.SimularAsync(3, 100));
            Assert.Equal("STATUS_INVALIDO", ex.Codigo);
        }

        // ---------- Alocação FEFO isolada ----------

        [Fact]
        public void Alocar_para_quando_a_necessidade_acaba()
        {
            var fila = new List<LoteInsumo>
        {
            Lote(1, 1, "A", Hoje.AddDays(10), 2m),
            Lote(2, 1, "B", Hoje.AddDays(20), 5m),
            Lote(3, 1, "C", Hoje.AddDays(30), 9m)
        };

            var alocado = CalculoProducao.Alocar(fila, 3m);   // CT-19: A tem 2 e B tem 5, precisa de 3

            Assert.Equal(2, alocado.Count);
            Assert.Equal(("A", 2m), (alocado[0].Lote.CodigoLote, alocado[0].Quantidade));
            Assert.Equal(("B", 1m), (alocado[1].Lote.CodigoLote, alocado[1].Quantidade));
        }
    }
}
