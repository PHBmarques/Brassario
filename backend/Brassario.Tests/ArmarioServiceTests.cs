using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    public class ArmarioServiceTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 19);

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => ArmarioServiceTests.Hoje;
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
                if (!string.IsNullOrWhiteSpace(busca))
                    q = q.Where(i => i.Nome.Contains(busca.Trim(), StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(q.OrderBy(i => i.Nome).ToList());
            }

            public Task<Insumo?> ObterPorIdAsync(long id, CancellationToken ct = default) => Task.FromResult(Dados.FirstOrDefault(x => x.Id == id));
            public Task<bool> ExisteNomeAsync(string n, long? ig, CancellationToken ct = default) => Task.FromResult(false);
            public Task AdicionarAsync(Insumo i, CancellationToken ct = default) { Dados.Add(i); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private class LoteRepoFalso : ILoteRepository
        {
            public List<LoteInsumo> Dados { get; } = new();

            public Task<bool> ExisteCodigoAsync(long insumoId, string codigo, CancellationToken ct = default) => Task.FromResult(false);
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
            private long _proximoLoteId = 1;

            public Cenario() => Servico = new ArmarioService(Insumos, Lotes, new RelogioFixo());

            public Insumo NovoInsumo(long id, string nome, decimal minimo = 10, CategoriaInsumo categoria = CategoriaInsumo.MALTE,
                int critica = 15, int atencao = 45, bool ativo = true)
            {
                var insumo = new Insumo
                {
                    Id = id,
                    Nome = nome,
                    Categoria = categoria,
                    UnidadeMedida = UnidadeMedida.KG,
                    EstoqueMinimo = minimo,
                    DiasValidadeCritica = critica,
                    DiasValidadeAtencao = atencao,
                    Ativo = ativo
                };
                Insumos.Dados.Add(insumo);
                return insumo;
            }

            public LoteInsumo NovoLote(long insumoId, int diasParaVencer, decimal saldo, int entradaHaDias = 5, string? codigo = null)
            {
                var lote = new LoteInsumo
                {
                    Id = _proximoLoteId++,
                    InsumoId = insumoId,
                    CodigoLote = codigo ?? $"L-{_proximoLoteId}",
                    DataEntrada = Hoje.AddDays(-entradaHaDias),
                    DataValidade = Hoje.AddDays(diasParaVencer),
                    QuantidadeInicial = Math.Max(saldo, 1),
                    QuantidadeAtual = saldo,
                    PrecoUnitario = 5
                };
                Lotes.Dados.Add(lote);
                return lote;
            }
        }

        // ---------- Resumo ----------

        [Fact]
        public async Task Saldo_soma_so_lotes_nao_vencidos_e_com_saldo()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen", minimo: 10);
            c.NovoLote(1, diasParaVencer: 60, saldo: 20);
            c.NovoLote(1, diasParaVencer: 100, saldo: 5);
            c.NovoLote(1, diasParaVencer: -3, saldo: 50);   // vencido: não entra
            c.NovoLote(1, diasParaVencer: 80, saldo: 0);    // esgotado: não entra

            var item = Assert.Single(await c.Servico.ListarAsync(null, null, false));

            Assert.Equal(25, item.Saldo);
            Assert.Equal(2, item.QuantidadeLotes);
            Assert.Equal(StatusEscassez.OK, item.StatusEscassez);
        }

        [Fact]
        public async Task Proximo_vencimento_e_o_do_lote_nao_vencido_mais_proximo()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoLote(1, diasParaVencer: 90, saldo: 10);
            c.NovoLote(1, diasParaVencer: 20, saldo: 10);
            c.NovoLote(1, diasParaVencer: -10, saldo: 10);  // vencido: ignorado

            var item = Assert.Single(await c.Servico.ListarAsync(null, null, false));

            Assert.Equal(Hoje.AddDays(20), item.ProximoVencimento);
            Assert.Equal(20, item.DiasParaVencer);
            Assert.Equal(SituacaoValidade.AMARELO, item.Cor);
        }

        [Theory]
        [InlineData(10, SituacaoValidade.VERMELHO)]
        [InlineData(15, SituacaoValidade.VERMELHO)]
        [InlineData(16, SituacaoValidade.AMARELO)]
        [InlineData(45, SituacaoValidade.AMARELO)]
        [InlineData(46, SituacaoValidade.VERDE)]
        public async Task Cor_segue_os_limites_padrao_do_insumo(int dias, SituacaoValidade esperada)
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoLote(1, dias, saldo: 10);

            var item = Assert.Single(await c.Servico.ListarAsync(null, null, false));
            Assert.Equal(esperada, item.Cor);
        }

        [Fact]
        public async Task Cor_usa_os_limites_proprios_de_cada_insumo()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Levedura US-05", critica: 5, atencao: 10, categoria: CategoriaInsumo.LEVEDURA);
            c.NovoInsumo(2, "Malte Pilsen");                                       // limites padrão 15/45
            c.NovoLote(1, diasParaVencer: 8, saldo: 10);                           // 8 dias: amarelo para a levedura
            c.NovoLote(2, diasParaVencer: 8, saldo: 10);                           // 8 dias: vermelho para o malte

            var itens = await c.Servico.ListarAsync(null, null, false);

            Assert.Equal(SituacaoValidade.AMARELO, itens.Single(i => i.InsumoId == 1).Cor);
            Assert.Equal(SituacaoValidade.VERMELHO, itens.Single(i => i.InsumoId == 2).Cor);
        }

        [Fact]
        public async Task Insumo_sem_lotes_validos_fica_VAZIO_sem_cor_nem_vencimento()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoLote(1, diasParaVencer: -1, saldo: 30); // só tem lote vencido

            var item = Assert.Single(await c.Servico.ListarAsync(null, null, false));

            Assert.Equal(0, item.Saldo);
            Assert.Equal(StatusEscassez.VAZIO, item.StatusEscassez);
            Assert.Null(item.ProximoVencimento);
            Assert.Null(item.DiasParaVencer);
            Assert.Null(item.Cor);
        }

        [Theory]
        [InlineData(25, StatusEscassez.OK)]
        [InlineData(10, StatusEscassez.OK)]      // exatamente o mínimo não é escassez
        [InlineData(9.5, StatusEscassez.BAIXO)]  // abaixo do mínimo
        [InlineData(0, StatusEscassez.VAZIO)]
        public async Task Status_de_escassez_compara_o_saldo_com_o_estoque_minimo(double saldo, StatusEscassez esperado)
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen", minimo: 10);
            if (saldo > 0) c.NovoLote(1, 60, (decimal)saldo);

            var item = Assert.Single(await c.Servico.ListarAsync(null, null, false));
            Assert.Equal(esperado, item.StatusEscassez);
        }

        [Fact]
        public async Task Lote_que_vence_hoje_ainda_conta_no_saldo()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoLote(1, diasParaVencer: 0, saldo: 7);

            var item = Assert.Single(await c.Servico.ListarAsync(null, null, false));
            Assert.Equal(7, item.Saldo);
            Assert.Equal(SituacaoValidade.VERMELHO, item.Cor);
        }

        // ---------- Filtros ----------

        [Fact]
        public async Task Filtra_por_categoria_e_por_busca()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoInsumo(2, "Lúpulo Cascade", categoria: CategoriaInsumo.LUPULO);
            c.NovoInsumo(3, "Lúpulo Citra", categoria: CategoriaInsumo.LUPULO);

            Assert.Equal(2, (await c.Servico.ListarAsync("lupulo", null, false)).Count);
            Assert.Single(await c.Servico.ListarAsync("LUPULO", "citra", false));
            Assert.Single(await c.Servico.ListarAsync(null, "pilsen", false));
        }

        [Fact]
        public async Task Categoria_invalida_vira_validacao_400()
        {
            var c = new Cenario();
            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Servico.ListarAsync("XYZ", null, false));
            Assert.Contains("categoria", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Insumo_inativo_so_aparece_quando_pedido()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoInsumo(2, "Malte Antigo", ativo: false);

            Assert.Single(await c.Servico.ListarAsync(null, null, false));
            Assert.Equal(2, (await c.Servico.ListarAsync(null, null, true)).Count);
        }

        // ---------- Detalhe ----------

        [Fact]
        public async Task Detalhe_lista_os_lotes_em_ordem_FEFO()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoLote(1, diasParaVencer: 60, saldo: 10, codigo: "C");
            c.NovoLote(1, diasParaVencer: 10, saldo: 10, codigo: "A");
            c.NovoLote(1, diasParaVencer: 30, saldo: 10, codigo: "B");

            var detalhe = await c.Servico.ObterDetalheAsync(1, false);

            Assert.Equal(new[] { "A", "B", "C" }, detalhe.Lotes.Select(l => l.CodigoLote));
            Assert.Equal(new int?[] { 1, 2, 3 }, detalhe.Lotes.Select(l => l.OrdemFefo));
        }

        [Fact]
        public async Task Detalhe_desempata_a_validade_pela_data_de_entrada()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoLote(1, diasParaVencer: 30, saldo: 10, entradaHaDias: 2, codigo: "RECENTE");
            c.NovoLote(1, diasParaVencer: 30, saldo: 10, entradaHaDias: 20, codigo: "ANTIGO");

            var detalhe = await c.Servico.ObterDetalheAsync(1, false);

            Assert.Equal(new[] { "ANTIGO", "RECENTE" }, detalhe.Lotes.Select(l => l.CodigoLote));
        }

        [Fact]
        public async Task Detalhe_coloca_lote_vencido_depois_da_fila_sem_posicao_FEFO()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoLote(1, diasParaVencer: -5, saldo: 10, codigo: "VENCIDO");
            c.NovoLote(1, diasParaVencer: 40, saldo: 10, codigo: "OK");

            var detalhe = await c.Servico.ObterDetalheAsync(1, false);

            Assert.Equal(new[] { "OK", "VENCIDO" }, detalhe.Lotes.Select(l => l.CodigoLote));
            Assert.Equal(1, detalhe.Lotes[0].OrdemFefo);
            Assert.False(detalhe.Lotes[0].Vencido);
            Assert.Null(detalhe.Lotes[1].OrdemFefo);
            Assert.True(detalhe.Lotes[1].Vencido);
            Assert.Equal(SituacaoValidade.VERMELHO, detalhe.Lotes[1].SituacaoValidade);

            // O lote vencido aparece na lista, mas não entra no saldo do resumo
            Assert.Equal(10, detalhe.Resumo.Saldo);
        }

        [Fact]
        public async Task Detalhe_esconde_lote_esgotado_por_padrao_e_mostra_quando_pedido()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Pilsen");
            c.NovoLote(1, diasParaVencer: 40, saldo: 10, codigo: "COM-SALDO");
            c.NovoLote(1, diasParaVencer: 20, saldo: 0, codigo: "ESGOTADO");

            Assert.Single((await c.Servico.ObterDetalheAsync(1, false)).Lotes);

            var completo = await c.Servico.ObterDetalheAsync(1, true);
            Assert.Equal(2, completo.Lotes.Count);
            Assert.Null(completo.Lotes.Single(l => l.CodigoLote == "ESGOTADO").OrdemFefo);
        }

        [Fact]
        public async Task Detalhe_traz_os_limites_do_insumo_e_o_resumo()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Levedura US-05", critica: 5, atencao: 10);
            c.NovoLote(1, 8, 4);

            var detalhe = await c.Servico.ObterDetalheAsync(1, false);

            Assert.Equal(5, detalhe.DiasValidadeCritica);
            Assert.Equal(10, detalhe.DiasValidadeAtencao);
            Assert.Equal(SituacaoValidade.AMARELO, detalhe.Resumo.Cor);
            Assert.Equal(StatusEscassez.BAIXO, detalhe.Resumo.StatusEscassez); // 4 < mínimo 10
        }

        [Fact]
        public async Task Detalhe_de_insumo_inexistente_vira_404()
        {
            var c = new Cenario();
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => c.Servico.ObterDetalheAsync(999, false));
        }
    }
}
