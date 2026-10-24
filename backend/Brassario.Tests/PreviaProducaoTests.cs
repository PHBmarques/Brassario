using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-21: prévia da produção (simula a baixa FEFO sem gravar) e o mesmo plano usado pela baixa.</summary>
    public class PreviaProducaoTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 24);

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => PreviaProducaoTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        }

        private class ReceitasStub : IReceitaService
        {
            public Receita Receita { get; set; } = null!;
            public Exception? Erro { get; set; }

            public Task<Receita> ObterParaProducaoAsync(long id, CancellationToken ct = default)
            {
                if (Erro is not null) throw Erro;
                return Task.FromResult(Receita);
            }

            public Task<List<ReceitaResumoResponse>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> ObterAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> CriarAsync(ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AtualizarAsync(long id, ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AlterarStatusAsync(long id, bool ativa, CancellationToken ct = default) => throw new NotSupportedException();
        }

        private class LoteRepoFalso : ILoteRepository
        {
            public List<LoteInsumo> Dados { get; } = new();
            public int Consultas { get; private set; }

            public Task<bool> ExisteCodigoAsync(long insumoId, string codigoLote, CancellationToken ct = default) => Task.FromResult(false);
            public Task<int> ProximoSequencialAsync(long insumoId, string prefixo, CancellationToken ct = default) => Task.FromResult(1);
            public Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default) { Dados.Add(lote); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) => throw new InvalidOperationException("A prévia não pode gravar.");

            public Task<List<LoteInsumo>> ListarPorInsumosAsync(IReadOnlyCollection<long> insumoIds, bool incluirEsgotados, CancellationToken ct = default)
            {
                Consultas++;
                return Task.FromResult(Dados.Where(l => insumoIds.Contains(l.InsumoId) && (incluirEsgotados || l.QuantidadeAtual > 0)).ToList());
            }
        }

        // Repositório da baixa (para provar que prévia e baixa mostram a mesma coisa)
        private class BateladasFalso : IBateladaRepository
        {
            public List<LoteInsumo> Lotes { get; } = new();
            public List<Batelada> Gravadas { get; } = new();
            private readonly List<Batelada> _pendentes = new();

            public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default) => await operacao();

            public Task<List<LoteInsumo>> BloquearLotesAsync(IReadOnlyCollection<long> insumoIds, CancellationToken ct = default) =>
                Task.FromResult(Lotes.Where(l => insumoIds.Contains(l.InsumoId) && l.QuantidadeAtual > 0)
                    .OrderBy(l => l.InsumoId).ThenBy(l => l.DataValidade).ThenBy(l => l.DataEntrada).ThenBy(l => l.Id).ToList());

            public Task<string> ProximoCodigoAsync(int ano, CancellationToken ct = default) => Task.FromResult($"B-{ano}-0001");
            public Task AdicionarAsync(Batelada batelada, CancellationToken ct = default) { _pendentes.Add(batelada); return Task.CompletedTask; }
            public Task<Batelada?> ObterComConsumosAsync(long id, CancellationToken ct = default) => Task.FromResult(Gravadas.FirstOrDefault(b => b.Id == id));

            public Task SalvarAsync(CancellationToken ct = default)
            {
                foreach (var b in _pendentes) { b.Id = Gravadas.Count + 1; Gravadas.Add(b); }
                _pendentes.Clear();
                return Task.CompletedTask;
            }
        }

        private static readonly Insumo Malte = new() { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG };
        private static readonly Insumo Lupulo = new() { Id = 2, Nome = "Lúpulo Cascade", UnidadeMedida = UnidadeMedida.KG };

        // Base de 100 L: 18 kg de malte e 0,3 kg de lúpulo
        private static Receita NovaReceita()
        {
            var receita = new Receita { Id = 3, Nome = "IPA Cascade", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { InsumoId = 1, Insumo = Malte, Quantidade = 18m });
            receita.Itens.Add(new ReceitaItem { InsumoId = 2, Insumo = Lupulo, Quantidade = 0.3m });
            return receita;
        }

        private static LoteInsumo Lote(Insumo insumo, long id, string codigo, int validadeEmDias, decimal saldo, decimal preco = 10m, int entradaHaDias = 30) => new()
        {
            Id = id, InsumoId = insumo.Id, Insumo = insumo, CodigoLote = codigo,
            DataEntrada = Hoje.AddDays(-entradaHaDias), DataValidade = Hoje.AddDays(validadeEmDias),
            QuantidadeInicial = saldo, QuantidadeAtual = saldo, PrecoUnitario = preco
        };

        private static (PreviaProducaoService Servico, LoteRepoFalso Lotes) Criar()
        {
            var lotes = new LoteRepoFalso();
            return (new PreviaProducaoService(new ReceitasStub { Receita = NovaReceita() }, lotes, new RelogioFixo()), lotes);
        }

        // ---------- Receita disponível ----------

        [Fact]
        public async Task Receita_disponivel_pode_confirmar_sem_problemas_e_com_custo_estimado()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-1", 60, 50m, preco: 8.5m));
            lotes.Dados.Add(Lote(Lupulo, 2, "L-1", 90, 1m, preco: 120m));

            var r = await servico.PreverAsync(3, 100);

            Assert.True(r.PodeConfirmar);
            Assert.Empty(r.Problemas);
            Assert.Equal(100m, r.Quantidade);
            Assert.All(r.Itens, i => Assert.Equal(SituacaoIngredientePrevia.DISPONIVEL, i.Situacao));

            // 18 x 8,5 + 0,3 x 120 = 153 + 36
            Assert.Equal(189m, r.CustoEstimado);

            // ordem alfabética dos insumos
            Assert.Equal(new[] { "Lúpulo Cascade", "Malte Pilsen" }, r.Itens.Select(i => i.Insumo));
            var malte = r.Itens.Single(i => i.InsumoId == 1);
            Assert.Equal((18m, 50m, "M-1", Hoje.AddDays(60), 18m), (malte.Necessario, malte.Disponivel, malte.Lote, malte.Validade, malte.SaiDoLote));
            Assert.Equal(UnidadeMedida.KG, malte.UnidadeMedida);
        }

        [Fact]
        public async Task Quantidade_diferente_da_base_escala_os_ingredientes()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-1", 60, 50m));
            lotes.Dados.Add(Lote(Lupulo, 2, "L-1", 90, 1m));

            var r = await servico.PreverAsync(3, 50);

            Assert.Equal(9m, r.Itens.Single(i => i.InsumoId == 1).Necessario);
            Assert.Equal(0.15m, r.Itens.Single(i => i.InsumoId == 2).Necessario);
        }

        // ---------- Receita com falta ----------

        [Fact]
        public async Task Receita_com_falta_nao_pode_confirmar_e_lista_o_que_falta_com_a_unidade()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-1", 60, 50m));
            lotes.Dados.Add(Lote(Lupulo, 2, "L-1", 90, 0.12m));   // precisa de 0,3

            var r = await servico.PreverAsync(3, 100);

            Assert.False(r.PodeConfirmar);

            var problema = Assert.Single(r.Problemas);
            Assert.Equal(TipoProblemaPrevia.FALTA, problema.Tipo);
            Assert.Equal("Lúpulo Cascade", problema.Insumo);
            Assert.Equal(0.18m, problema.Falta);
            Assert.Equal(UnidadeMedida.KG, problema.UnidadeMedida);

            var linha = r.Itens.Single(i => i.InsumoId == 2);
            Assert.Equal(SituacaoIngredientePrevia.FALTA_ESTOQUE, linha.Situacao);
            Assert.Equal((0.3m, 0.12m), (linha.Necessario, linha.Disponivel));
            Assert.Null(linha.Lote);
            Assert.Null(linha.Validade);
            Assert.Equal(0m, linha.SaiDoLote);   // nada sai de um ingrediente em falta

            // o ingrediente com estoque continua DISPONIVEL
            Assert.Equal(SituacaoIngredientePrevia.DISPONIVEL, r.Itens.Single(i => i.InsumoId == 1).Situacao);
        }

        [Fact]
        public async Task Insumo_sem_nenhum_lote_aparece_em_falta_com_disponivel_zero()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-1", 60, 50m));   // lúpulo sem lote

            var r = await servico.PreverAsync(3, 100);

            var linha = r.Itens.Single(i => i.InsumoId == 2);
            Assert.Equal(0m, linha.Disponivel);
            Assert.Equal(0.3m, Assert.Single(r.Problemas).Falta);
        }

        // ---------- Prévia que passa por dois lotes ----------

        [Fact]
        public async Task Ingrediente_que_passa_por_dois_lotes_vira_duas_linhas_na_ordem_FEFO()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-TARDE", 200, 40m, preco: 9.2m));
            lotes.Dados.Add(Lote(Malte, 2, "M-CEDO", 30, 10m, preco: 8.5m));
            lotes.Dados.Add(Lote(Lupulo, 3, "L-1", 90, 1m, preco: 120m));

            var r = await servico.PreverAsync(3, 100);

            var malte = r.Itens.Where(i => i.InsumoId == 1).ToList();
            Assert.Equal(2, malte.Count);
            Assert.Equal(new[] { ("M-CEDO", 10m), ("M-TARDE", 8m) }, malte.Select(l => (l.Lote!, l.SaiDoLote)));
            Assert.All(malte, l => Assert.Equal((18m, 50m), (l.Necessario, l.Disponivel)));   // disponível é o saldo total do insumo
            Assert.Equal(Hoje.AddDays(30), malte[0].Validade);
            Assert.True(r.PodeConfirmar);

            // 10 x 8,5 + 8 x 9,2 + 0,3 x 120 = 85 + 73,6 + 36
            Assert.Equal(194.6m, r.CustoEstimado);
        }

        [Fact]
        public async Task Empate_de_validade_sai_primeiro_o_lote_que_entrou_antes()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-NOVO", 30, 100m, entradaHaDias: 2));
            lotes.Dados.Add(Lote(Malte, 2, "M-VELHO", 30, 100m, entradaHaDias: 20));
            lotes.Dados.Add(Lote(Lupulo, 3, "L-1", 90, 1m));

            var r = await servico.PreverAsync(3, 100);

            Assert.Equal("M-VELHO", Assert.Single(r.Itens, i => i.InsumoId == 1).Lote);
        }

        // ---------- Lote vencido ----------

        [Fact]
        public async Task So_lote_vencido_gera_problema_VENCIDO_com_lote_e_validade()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-VELHO", -3, 100m));
            lotes.Dados.Add(Lote(Malte, 2, "M-VELHO-2", -1, 20m));
            lotes.Dados.Add(Lote(Lupulo, 3, "L-1", 90, 1m));

            var r = await servico.PreverAsync(3, 100);

            Assert.False(r.PodeConfirmar);
            Assert.Equal(2, r.Problemas.Count);
            Assert.All(r.Problemas, p => Assert.Equal(TipoProblemaPrevia.VENCIDO, p.Tipo));
            Assert.Equal(new[] { "M-VELHO", "M-VELHO-2" }, r.Problemas.Select(p => p.Lote!));
            Assert.Equal(Hoje.AddDays(-3), r.Problemas[0].Validade);
            Assert.All(r.Problemas, p => Assert.Null(p.Falta));

            var linha = r.Itens.Single(i => i.InsumoId == 1);
            Assert.Equal(SituacaoIngredientePrevia.FALTA_ESTOQUE, linha.Situacao);
            Assert.Equal(0m, linha.Disponivel);   // o vencido não conta como saldo
        }

        [Fact]
        public async Task Lote_vencido_junto_de_lote_valido_suficiente_e_ignorado_sem_problema()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-VENCIDO", -1, 100m));
            lotes.Dados.Add(Lote(Malte, 2, "M-OK", 60, 30m));
            lotes.Dados.Add(Lote(Lupulo, 3, "L-1", 90, 1m));

            var r = await servico.PreverAsync(3, 100);

            Assert.True(r.PodeConfirmar);
            Assert.Empty(r.Problemas);
            Assert.Equal("M-OK", Assert.Single(r.Itens, i => i.InsumoId == 1).Lote);
        }

        [Fact]
        public async Task Lote_vencido_junto_de_lote_valido_que_nao_basta_e_falta_e_nao_vencido()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-VENCIDO", -1, 100m));
            lotes.Dados.Add(Lote(Malte, 2, "M-OK", 60, 5m));   // só 5 de 18
            lotes.Dados.Add(Lote(Lupulo, 3, "L-1", 90, 1m));

            var r = await servico.PreverAsync(3, 100);

            var problema = Assert.Single(r.Problemas);
            Assert.Equal(TipoProblemaPrevia.FALTA, problema.Tipo);
            Assert.Equal(13m, problema.Falta);
        }

        // ---------- Não grava e valida ----------

        [Fact]
        public async Task A_previa_nao_altera_nenhum_saldo()
        {
            var (servico, lotes) = Criar();
            lotes.Dados.Add(Lote(Malte, 1, "M-1", 60, 50m));
            lotes.Dados.Add(Lote(Lupulo, 2, "L-1", 90, 1m));

            await servico.PreverAsync(3, 100);
            await servico.PreverAsync(3, 100);   // repetir dá o mesmo resultado

            Assert.Equal(new[] { 50m, 1m }, lotes.Dados.Select(l => l.QuantidadeAtual));
            Assert.All(lotes.Dados, l => Assert.Equal(0, l.Versao));
            Assert.Equal(2, lotes.Consultas);   // uma consulta de leitura por prévia
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0.0)]
        [InlineData(-5.0)]
        [InlineData(0.004)]
        public async Task Quantidade_ausente_zero_negativa_ou_minuscula_gera_400(double? quantidade)
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.PreverAsync(3, quantidade.HasValue ? (decimal)quantidade.Value : null));

            Assert.True(ex.Detalhes.ContainsKey("quantidade"));
        }

        [Fact]
        public async Task Receita_inexistente_ou_inativa_propaga_o_erro_da_receita()
        {
            var lotes = new LoteRepoFalso();
            var receitas = new ReceitasStub { Erro = new RecursoNaoEncontradoException("Receita não encontrada.") };
            var servico = new PreviaProducaoService(receitas, lotes, new RelogioFixo());

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.PreverAsync(3, 100));
            Assert.Equal(0, lotes.Consultas);
        }

        // ---------- Mesmo cálculo da baixa ----------

        [Fact]
        public async Task O_que_a_previa_mostra_e_exatamente_o_que_a_baixa_desconta()
        {
            var receita = NovaReceita();
            var lotes = new[]
            {
                Lote(Malte, 1, "M-TARDE", 200, 40m, preco: 9.2m),
                Lote(Malte, 2, "M-CEDO", 30, 10m, preco: 8.5m),
                Lote(Malte, 3, "M-VENCIDO", -1, 100m),
                Lote(Lupulo, 4, "L-B", 120, 0.2m, preco: 110m),
                Lote(Lupulo, 5, "L-A", 60, 0.2m, preco: 120m)
            };

            var leitura = new LoteRepoFalso();
            leitura.Dados.AddRange(lotes);
            var previa = await new PreviaProducaoService(new ReceitasStub { Receita = receita }, leitura, new RelogioFixo()).PreverAsync(3, 100);

            var repo = new BateladasFalso();
            repo.Lotes.AddRange(lotes);
            var baixa = await new BateladaService(new ReceitasStub { Receita = receita }, repo, new RelogioFixo())
                .ProduzirAsync(new BateladaRequest { ReceitaId = 3, VolumePlanejado = 100 }, 7);

            var daPrevia = previa.Itens.Select(l => (l.InsumoId, l.Lote, l.SaiDoLote)).OrderBy(x => x.InsumoId).ThenBy(x => x.Lote).ToList();
            var daBaixa = baixa.Consumos.Select(c => (c.InsumoId, (string?)c.CodigoLote, c.Quantidade)).OrderBy(x => x.InsumoId).ThenBy(x => x.Item2).ToList();

            Assert.Equal(daBaixa, daPrevia);
            Assert.Equal(baixa.CustoTotal, previa.CustoEstimado);
        }

        [Fact]
        public async Task Quando_a_previa_nao_deixa_confirmar_a_baixa_tambem_recusa()
        {
            var receita = NovaReceita();
            var lotes = new[] { Lote(Malte, 1, "M-1", 60, 50m), Lote(Lupulo, 2, "L-1", 90, 0.12m) };

            var leitura = new LoteRepoFalso();
            leitura.Dados.AddRange(lotes);
            var previa = await new PreviaProducaoService(new ReceitasStub { Receita = receita }, leitura, new RelogioFixo()).PreverAsync(3, 100);

            var repo = new BateladasFalso();
            repo.Lotes.AddRange(lotes);
            var servico = new BateladaService(new ReceitasStub { Receita = receita }, repo, new RelogioFixo());

            Assert.False(previa.PodeConfirmar);
            await Assert.ThrowsAsync<EstoqueInsuficienteException>(() =>
                servico.ProduzirAsync(new BateladaRequest { ReceitaId = 3, VolumePlanejado = 100 }, 7));
        }
    }
}
