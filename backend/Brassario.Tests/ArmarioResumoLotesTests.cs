using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-19: resumo por insumo (GET /insumos/resumo) e lotes na ordem de uso (GET /insumos/{id}/lotes).</summary>
    public class ArmarioResumoLotesTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 19);

        // ---------- Fakes em memória ----------

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => ArmarioResumoLotesTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        }

        private class InsumoRepoFalso : IInsumoRepository
        {
            public List<Insumo> Dados { get; } = new();

            public Task<List<Insumo>> ListarAsync(CategoriaInsumo? categoria, string? busca, bool incluirInativos, CancellationToken ct = default)
            {
                var q = Dados.AsEnumerable();
                if (!incluirInativos) q = q.Where(i => i.Ativo);
                if (categoria.HasValue) q = q.Where(i => i.Categoria == categoria.Value);
                if (!string.IsNullOrWhiteSpace(busca)) q = q.Where(i => i.Nome.Contains(busca.Trim(), StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(q.OrderBy(i => i.Nome).ToList());
            }

            public Task<Insumo?> ObterPorIdAsync(long id, CancellationToken ct = default) => Task.FromResult(Dados.FirstOrDefault(i => i.Id == id));
            public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default) => Task.FromResult(false);
            public Task AdicionarAsync(Insumo insumo, CancellationToken ct = default) { Dados.Add(insumo); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private class LoteRepoFalso : ILoteRepository
        {
            public List<LoteInsumo> Dados { get; } = new();

            public Task<bool> ExisteCodigoAsync(long insumoId, string codigoLote, CancellationToken ct = default) => Task.FromResult(false);
            public Task<int> ProximoSequencialAsync(long insumoId, string prefixo, CancellationToken ct = default) => Task.FromResult(1);
            public Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default) { Dados.Add(lote); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) => Task.CompletedTask;

            public Task<List<LoteInsumo>> ListarPorInsumosAsync(IReadOnlyCollection<long> insumoIds, bool incluirEsgotados, CancellationToken ct = default) =>
                Task.FromResult(Dados.Where(l => insumoIds.Contains(l.InsumoId) && (incluirEsgotados || l.QuantidadeAtual > 0)).ToList());
        }

        private class Cenario
        {
            public InsumoRepoFalso Insumos { get; } = new();
            public LoteRepoFalso Lotes { get; } = new();
            public ArmarioService Servico { get; }
            private long _proximoLote = 1;

            public Cenario() => Servico = new ArmarioService(Insumos, Lotes, new RelogioFixo());

            public Insumo NovoInsumo(long id = 1, string nome = "Malte Pilsen", decimal minimo = 0, int critica = 15, int atencao = 45,
                CategoriaInsumo categoria = CategoriaInsumo.MALTE, bool ativo = true)
            {
                var insumo = new Insumo
                {
                    Id = id, Nome = nome, Categoria = categoria, UnidadeMedida = UnidadeMedida.KG,
                    EstoqueMinimo = minimo, DiasValidadeCritica = critica, DiasValidadeAtencao = atencao, Ativo = ativo
                };
                Insumos.Dados.Add(insumo);
                return insumo;
            }

            public LoteInsumo NovoLote(long insumoId, decimal saldo, int validadeEmDias, int entradaHaDias = 10, string? codigo = null,
                Fornecedor? fornecedor = null, string? notaFiscal = null, decimal inicial = 100, decimal preco = 8.5m)
            {
                var id = _proximoLote++;
                var lote = new LoteInsumo
                {
                    Id = id, InsumoId = insumoId, CodigoLote = codigo ?? $"L-{id}",
                    DataEntrada = Hoje.AddDays(-entradaHaDias), DataValidade = Hoje.AddDays(validadeEmDias),
                    QuantidadeInicial = inicial, QuantidadeAtual = saldo, PrecoUnitario = preco,
                    Fornecedor = fornecedor, FornecedorId = fornecedor?.Id, NotaFiscal = notaFiscal
                };
                Lotes.Dados.Add(lote);
                return lote;
            }
        }

        // ---------- GET /insumos/{id}/lotes: ordem de uso ----------

        [Fact]
        public async Task Tres_lotes_saem_na_ordem_de_uso_validade_mais_proxima_primeiro()
        {
            var c = new Cenario();
            c.NovoInsumo();
            var longe = c.NovoLote(1, 10, validadeEmDias: 90);
            var perto = c.NovoLote(1, 10, validadeEmDias: 30);
            var meio = c.NovoLote(1, 10, validadeEmDias: 60);

            var r = await c.Servico.ObterLotesDoInsumoAsync(1);

            Assert.Equal(new[] { perto.Id, meio.Id, longe.Id }, r.Lotes.Select(l => l.Id));
            Assert.Equal(new int?[] { 1, 2, 3 }, r.Lotes.Select(l => l.OrdemDeUso));
        }

        [Fact]
        public async Task Empate_de_validade_usa_primeiro_o_lote_que_entrou_antes()
        {
            var c = new Cenario();
            c.NovoInsumo();
            var entrouDepois = c.NovoLote(1, 10, validadeEmDias: 30, entradaHaDias: 2);
            var entrouAntes = c.NovoLote(1, 10, validadeEmDias: 30, entradaHaDias: 20);

            var r = await c.Servico.ObterLotesDoInsumoAsync(1);

            Assert.Equal(new[] { entrouAntes.Id, entrouDepois.Id }, r.Lotes.Select(l => l.Id));
        }

        [Fact]
        public async Task Empate_de_validade_e_de_entrada_usa_o_menor_id()
        {
            var c = new Cenario();
            c.NovoInsumo();
            var primeiro = c.NovoLote(1, 10, validadeEmDias: 30, entradaHaDias: 5);
            var segundo = c.NovoLote(1, 10, validadeEmDias: 30, entradaHaDias: 5);

            var r = await c.Servico.ObterLotesDoInsumoAsync(1);

            Assert.Equal(new[] { primeiro.Id, segundo.Id }, r.Lotes.Select(l => l.Id));
        }

        [Fact]
        public async Task Lote_esgotado_fica_fora_da_lista_principal_e_vai_para_a_lista_de_esgotados()
        {
            var c = new Cenario();
            c.NovoInsumo();
            var ativo = c.NovoLote(1, 10, validadeEmDias: 30);
            var esgotado = c.NovoLote(1, 0, validadeEmDias: 10);   // venceria antes, mas já zerou

            var r = await c.Servico.ObterLotesDoInsumoAsync(1);

            Assert.Equal(ativo.Id, Assert.Single(r.Lotes).Id);
            Assert.Equal(1, r.Lotes[0].OrdemDeUso);                // o esgotado não ocupa posição na fila
            Assert.Equal(esgotado.Id, Assert.Single(r.Esgotados).Id);
            Assert.Null(r.Esgotados[0].OrdemDeUso);
        }

        [Fact]
        public async Task Esgotados_vem_do_mais_recente_para_o_mais_antigo()
        {
            var c = new Cenario();
            c.NovoInsumo();
            var antigo = c.NovoLote(1, 0, validadeEmDias: 30, entradaHaDias: 60);
            var recente = c.NovoLote(1, 0, validadeEmDias: 30, entradaHaDias: 5);

            var r = await c.Servico.ObterLotesDoInsumoAsync(1);

            Assert.Equal(new[] { recente.Id, antigo.Id }, r.Esgotados.Select(l => l.Id));
        }

        [Fact]
        public async Task Insumo_sem_lote_devolve_listas_vazias_e_nao_da_erro()
        {
            var c = new Cenario();
            c.NovoInsumo();

            var r = await c.Servico.ObterLotesDoInsumoAsync(1);

            Assert.Empty(r.Lotes);
            Assert.Empty(r.Esgotados);
            Assert.Equal(0m, r.Resumo.SaldoTotal);
        }

        [Fact]
        public async Task Cada_lote_traz_codigo_fornecedor_nota_fiscal_datas_quantidades_preco_e_situacao()
        {
            var c = new Cenario();
            c.NovoInsumo();
            var lote = c.NovoLote(1, saldo: 60, validadeEmDias: 30, entradaHaDias: 7, codigo: "ABC-9",
                fornecedor: new Fornecedor { Id = 4, Nome = "Maltaria Sul", Ativo = true }, notaFiscal: "NF-123", inicial: 100, preco: 9.75m);

            var item = Assert.Single((await c.Servico.ObterLotesDoInsumoAsync(1)).Lotes);

            Assert.Equal(lote.Id, item.Id);
            Assert.Equal("ABC-9", item.CodigoLote);
            Assert.Equal(4, item.FornecedorId);
            Assert.Equal("Maltaria Sul", item.FornecedorNome);
            Assert.Equal("NF-123", item.NotaFiscal);
            Assert.Equal(Hoje.AddDays(-7), item.DataEntrada);
            Assert.Equal(Hoje.AddDays(30), item.DataValidade);
            Assert.Equal(100m, item.QuantidadeInicial);
            Assert.Equal(60m, item.QuantidadeAtual);
            Assert.Equal(9.75m, item.PrecoUnitario);
            Assert.Equal(30, item.DiasParaVencer);
            Assert.Equal(SituacaoValidade.AMARELO, item.SituacaoValidade);   // 30 dias: entre 15 e 45
            Assert.False(item.Vencido);
        }

        [Fact]
        public async Task Lote_sem_fornecedor_e_sem_nota_fiscal_vem_com_esses_campos_nulos()
        {
            var c = new Cenario();
            c.NovoInsumo();
            c.NovoLote(1, 10, validadeEmDias: 30);

            var item = Assert.Single((await c.Servico.ObterLotesDoInsumoAsync(1)).Lotes);

            Assert.Null(item.FornecedorId);
            Assert.Null(item.FornecedorNome);
            Assert.Null(item.NotaFiscal);
        }

        [Fact]
        public async Task Lote_vencido_com_saldo_vai_para_o_fim_da_lista_principal_sem_ordem_de_uso()
        {
            var c = new Cenario();
            c.NovoInsumo();
            var bom = c.NovoLote(1, 10, validadeEmDias: 30);
            var vencido = c.NovoLote(1, 5, validadeEmDias: -3);

            var r = await c.Servico.ObterLotesDoInsumoAsync(1);

            Assert.Equal(new[] { bom.Id, vencido.Id }, r.Lotes.Select(l => l.Id));
            Assert.Equal(1, r.Lotes[0].OrdemDeUso);
            Assert.Null(r.Lotes[1].OrdemDeUso);
            Assert.True(r.Lotes[1].Vencido);
            Assert.Equal(-3, r.Lotes[1].DiasParaVencer);
            Assert.Empty(r.Esgotados);   // tem saldo, então não é esgotado
        }

        [Fact]
        public async Task Lotes_de_outro_insumo_nao_aparecem()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoInsumo(2, "Lúpulo Cascade");
            var meu = c.NovoLote(1, 10, validadeEmDias: 30);
            c.NovoLote(2, 10, validadeEmDias: 20);

            Assert.Equal(meu.Id, Assert.Single((await c.Servico.ObterLotesDoInsumoAsync(1)).Lotes).Id);
        }

        [Fact]
        public async Task Insumo_inexistente_vira_404()
        {
            var c = new Cenario();
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => c.Servico.ObterLotesDoInsumoAsync(999));
        }

        [Fact]
        public async Task A_ordem_do_armario_e_exatamente_a_fila_que_a_baixa_usa()
        {
            var c = new Cenario();
            c.NovoInsumo();
            c.NovoLote(1, 10, validadeEmDias: 90, entradaHaDias: 1);
            c.NovoLote(1, 10, validadeEmDias: 30, entradaHaDias: 9);
            c.NovoLote(1, 10, validadeEmDias: 30, entradaHaDias: 3);
            c.NovoLote(1, 10, validadeEmDias: 60, entradaHaDias: 5);
            c.NovoLote(1, 10, validadeEmDias: -2, entradaHaDias: 50);   // vencido: fora da fila
            c.NovoLote(1, 0, validadeEmDias: 5, entradaHaDias: 40);     // esgotado: fora da fila

            var r = await c.Servico.ObterLotesDoInsumoAsync(1);
            var filaDaBaixa = CalculoProducao.FilaFefo(c.Lotes.Dados, Hoje).Select(l => l.Id).ToList();

            var naTela = r.Lotes.Where(l => l.OrdemDeUso.HasValue).Select(l => l.Id).ToList();
            Assert.Equal(filaDaBaixa, naTela);
            Assert.Equal(4, naTela.Count);
        }

        // ---------- GET /insumos/resumo ----------

        [Fact]
        public async Task Insumo_recem_cadastrado_sem_lote_aparece_com_saldo_zero_em_vez_de_sumir()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoInsumo(2, "Lúpulo Novo");   // sem lote nenhum
            c.NovoLote(1, 10, validadeEmDias: 30);

            var lista = await c.Servico.ResumirInsumosAsync(null, null, false);

            Assert.Equal(2, lista.Count);
            var novo = lista.Single(i => i.InsumoId == 2);
            Assert.Equal(0m, novo.SaldoTotal);
            Assert.Equal(0m, novo.SaldoUtilizavel);
            Assert.Equal(0, novo.QuantidadeLotes);
            Assert.Null(novo.ValidadeMaisProxima);
            Assert.Null(novo.DiasParaVencer);
        }

        [Fact]
        public async Task Resumo_traz_saldo_total_numero_de_lotes_com_saldo_e_validade_mais_proxima()
        {
            var c = new Cenario();
            c.NovoInsumo();
            c.NovoLote(1, 10, validadeEmDias: 90);
            c.NovoLote(1, 25.5m, validadeEmDias: 40);
            c.NovoLote(1, 0, validadeEmDias: 5);   // esgotado: não conta

            var item = Assert.Single(await c.Servico.ResumirInsumosAsync(null, null, false));

            Assert.Equal(35.5m, item.SaldoTotal);
            Assert.Equal(35.5m, item.SaldoUtilizavel);
            Assert.Equal(2, item.QuantidadeLotes);
            Assert.Equal(Hoje.AddDays(40), item.ValidadeMaisProxima);
            Assert.Equal(40, item.DiasParaVencer);
        }

        [Fact]
        public async Task Lote_vencido_conta_no_saldo_total_mas_nao_no_utilizavel()
        {
            var c = new Cenario();
            c.NovoInsumo();
            c.NovoLote(1, 10, validadeEmDias: 90);
            c.NovoLote(1, 4, validadeEmDias: -1);

            var item = Assert.Single(await c.Servico.ResumirInsumosAsync(null, null, false));

            Assert.Equal(14m, item.SaldoTotal);
            Assert.Equal(10m, item.SaldoUtilizavel);
            Assert.Equal(2, item.QuantidadeLotes);
            Assert.Equal(Hoje.AddDays(-1), item.ValidadeMaisProxima);   // a mais próxima é a do vencido
            Assert.Equal(-1, item.DiasParaVencer);
        }

        // Limites do insumo: crítica 15 dias, atenção 45 dias, mínimo 100
        [Theory]
        [InlineData(200, 120, SituacaoInsumo.OK, false)]
        [InlineData(46, 120, SituacaoInsumo.OK, false)]
        [InlineData(45, 120, SituacaoInsumo.ATENCAO, false)]      // até 45 dias: atenção
        [InlineData(16, 120, SituacaoInsumo.ATENCAO, false)]
        [InlineData(15, 120, SituacaoInsumo.CRITICO, false)]      // até 15 dias: crítico
        [InlineData(0, 120, SituacaoInsumo.CRITICO, false)]       // vence hoje ainda é utilizável: crítico
        [InlineData(-1, 120, SituacaoInsumo.VENCIDO, false)]
        [InlineData(200, 99, SituacaoInsumo.ABAIXO_DO_MINIMO, true)]
        [InlineData(200, 100, SituacaoInsumo.OK, false)]          // igual ao mínimo não é abaixo
        // Precedência quando mais de uma vale: VENCIDO > CRITICO > ABAIXO_DO_MINIMO > ATENCAO
        [InlineData(10, 50, SituacaoInsumo.CRITICO, true)]
        [InlineData(30, 50, SituacaoInsumo.ABAIXO_DO_MINIMO, true)]
        [InlineData(-5, 50, SituacaoInsumo.VENCIDO, true)]
        public async Task Situacao_do_insumo_pelos_limites_de_validade_e_pelo_minimo(
            int validadeEmDias, decimal saldo, SituacaoInsumo esperada, bool abaixoDoMinimo)
        {
            var c = new Cenario();
            c.NovoInsumo(minimo: 100, critica: 15, atencao: 45);
            c.NovoLote(1, saldo, validadeEmDias);

            var item = Assert.Single(await c.Servico.ResumirInsumosAsync(null, null, false));

            // Lote vencido não é utilizável: nesse caso o saldo utilizável é zero e fica abaixo do mínimo
            var abaixoEsperado = validadeEmDias < 0 || abaixoDoMinimo;
            Assert.Equal(esperada, item.Situacao);
            Assert.Equal(abaixoEsperado, item.AbaixoDoMinimo);
        }

        [Fact]
        public async Task Situacao_usa_os_dias_de_cada_insumo_e_nao_valores_fixos()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Levedura", critica: 5, atencao: 10);
            c.NovoInsumo(2, "Malte", critica: 30, atencao: 90);
            c.NovoLote(1, 10, validadeEmDias: 20);   // levedura: 20 dias está longe do alerta
            c.NovoLote(2, 10, validadeEmDias: 20);   // malte: 20 dias já é crítico

            var lista = await c.Servico.ResumirInsumosAsync(null, null, false);

            Assert.Equal(SituacaoInsumo.OK, lista.Single(i => i.InsumoId == 1).Situacao);
            Assert.Equal(SituacaoInsumo.CRITICO, lista.Single(i => i.InsumoId == 2).Situacao);
        }

        [Fact]
        public async Task Insumo_sem_lote_com_estoque_minimo_aparece_como_ABAIXO_DO_MINIMO()
        {
            var c = new Cenario();
            c.NovoInsumo(minimo: 50);

            var item = Assert.Single(await c.Servico.ResumirInsumosAsync(null, null, false));

            Assert.Equal(SituacaoInsumo.ABAIXO_DO_MINIMO, item.Situacao);
            Assert.True(item.AbaixoDoMinimo);
        }

        [Fact]
        public async Task Insumo_sem_lote_e_sem_estoque_minimo_aparece_como_OK()
        {
            var c = new Cenario();
            c.NovoInsumo(minimo: 0);

            var item = Assert.Single(await c.Servico.ResumirInsumosAsync(null, null, false));

            Assert.Equal(SituacaoInsumo.OK, item.Situacao);
            Assert.False(item.AbaixoDoMinimo);
        }

        [Fact]
        public async Task Resumo_filtra_por_categoria_busca_e_inativos()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen", categoria: CategoriaInsumo.MALTE);
            c.NovoInsumo(2, "Lúpulo Cascade", categoria: CategoriaInsumo.LUPULO);
            c.NovoInsumo(3, "Malte Antigo", categoria: CategoriaInsumo.MALTE, ativo: false);

            Assert.Equal(new long[] { 2 }, (await c.Servico.ResumirInsumosAsync("LUPULO", null, false)).Select(i => i.InsumoId));
            Assert.Equal(new long[] { 1 }, (await c.Servico.ResumirInsumosAsync(null, "malte", false)).Select(i => i.InsumoId));
            Assert.Equal(new long[] { 3, 1 }, (await c.Servico.ResumirInsumosAsync(null, "malte", true)).Select(i => i.InsumoId));
        }

        [Fact]
        public async Task Categoria_invalida_vira_400()
        {
            var c = new Cenario();
            await Assert.ThrowsAsync<ValidacaoException>(() => c.Servico.ResumirInsumosAsync("BEBIDA", null, false));
        }

        [Fact]
        public async Task Resumo_sem_nenhum_insumo_devolve_lista_vazia()
        {
            var c = new Cenario();
            Assert.Empty(await c.Servico.ResumirInsumosAsync(null, null, false));
        }

        // ---------- Regra única da ordem de uso ----------

        [Fact]
        public void OrdemDeUso_ordena_por_validade_depois_entrada_depois_id()
        {
            LoteInsumo L(long id, int validade, int entradaHa) => new()
            {
                Id = id, DataValidade = Hoje.AddDays(validade), DataEntrada = Hoje.AddDays(-entradaHa), QuantidadeAtual = 1
            };

            var ordem = CalculoProducao.OrdemDeUso(new[] { L(5, 30, 1), L(4, 30, 9), L(3, 10, 2), L(2, 30, 9) }).Select(l => l.Id);

            Assert.Equal(new long[] { 3, 2, 4, 5 }, ordem);
        }

        [Theory]
        [InlineData(0, 10, true)]     // vence hoje: ainda pode
        [InlineData(1, 10, true)]
        [InlineData(-1, 10, false)]   // vencido
        [InlineData(10, 0, false)]    // esgotado
        public void PodeSerConsumido_exige_saldo_e_validade_de_hoje_em_diante(int validadeEmDias, decimal saldo, bool esperado)
        {
            var lote = new LoteInsumo { DataValidade = Hoje.AddDays(validadeEmDias), QuantidadeAtual = saldo };
            Assert.Equal(esperado, CalculoProducao.PodeSerConsumido(lote, Hoje));
        }
    }
}
