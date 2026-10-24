using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-23: GET /alertas (tipo, ação, ids, ordem, filtros) e a perda que faz o alerta sumir.</summary>
    public class AlertaServiceTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 24);

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => AlertaServiceTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        }

        private class InsumoRepoFalso : IInsumoRepository
        {
            public List<Insumo> Dados { get; } = new();

            public Task<List<Insumo>> ListarAsync(CategoriaInsumo? categoria, string? busca, bool incluirInativos, CancellationToken ct = default) =>
                Task.FromResult(Dados.Where(i => incluirInativos || i.Ativo).OrderBy(i => i.Nome).ToList());

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

        // Perda em memória sobre a MESMA lista de lotes do alerta (para provar que o alerta some)
        private class PerdaRepoFalso : IPerdaRepository
        {
            private readonly List<LoteInsumo> _lotes;
            private readonly List<Insumo> _insumos;
            public List<MovimentacaoEstoque> Movimentacoes { get; } = new();

            public PerdaRepoFalso(List<LoteInsumo> lotes, List<Insumo> insumos) { _lotes = lotes; _insumos = insumos; }

            public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default) => await operacao();
            public Task<LoteInsumo?> BloquearLoteAsync(long loteId, CancellationToken ct = default) => Task.FromResult(_lotes.FirstOrDefault(l => l.Id == loteId));
            public Task<Insumo?> ObterInsumoAsync(long insumoId, CancellationToken ct = default) => Task.FromResult(_insumos.FirstOrDefault(i => i.Id == insumoId));
            public Task AdicionarMovimentacaoAsync(MovimentacaoEstoque movimentacao, CancellationToken ct = default) { Movimentacoes.Add(movimentacao); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private class Cenario
        {
            public InsumoRepoFalso Insumos { get; } = new();
            public LoteRepoFalso Lotes { get; } = new();
            public AlertasService Servico { get; }
            private long _proximoLote = 1;

            public Cenario() => Servico = new AlertasService(Insumos, Lotes, new RelogioFixo());

            public Insumo NovoInsumo(long id, string nome, decimal minimo = 0, int critica = 15, int atencao = 45,
                UnidadeMedida unidade = UnidadeMedida.KG, bool ativo = true)
            {
                var insumo = new Insumo
                {
                    Id = id, Nome = nome, Categoria = CategoriaInsumo.MALTE, UnidadeMedida = unidade,
                    EstoqueMinimo = minimo, DiasValidadeCritica = critica, DiasValidadeAtencao = atencao, Ativo = ativo
                };
                Insumos.Dados.Add(insumo);
                return insumo;
            }

            public LoteInsumo NovoLote(long insumoId, int validadeEmDias, decimal saldo, string? codigo = null)
            {
                var id = _proximoLote++;
                var lote = new LoteInsumo
                {
                    Id = id, InsumoId = insumoId, CodigoLote = codigo ?? $"L-{id}",
                    DataEntrada = Hoje.AddDays(-60), DataValidade = Hoje.AddDays(validadeEmDias),
                    QuantidadeInicial = 100m, QuantidadeAtual = saldo, PrecoUnitario = 10m
                };
                Lotes.Dados.Add(lote);
                return lote;
            }

            /// <summary>Um insumo para cada tipo de alerta, todos com os limites padrão (15 e 45 dias).</summary>
            public (LoteInsumo Vencido, LoteInsumo Critico, LoteInsumo Atencao) UmDeCadaTipo()
            {
                NovoInsumo(1, "Levedura US-05", unidade: UnidadeMedida.G);
                var vencido = NovoLote(1, -2, 5m, "Y-VENCIDO");
                NovoLote(1, 200, 50m);                              // lote bom: o insumo continua com saldo

                NovoInsumo(2, "Malte Pilsen");
                var critico = NovoLote(2, 10, 50m, "M-CRITICO");

                NovoInsumo(3, "Lúpulo Cascade");
                var atencao = NovoLote(3, 30, 20m, "L-ATENCAO");

                NovoInsumo(4, "Adjunto Aveia");                      // sem nenhum lote: estoque zerado

                NovoInsumo(5, "Sal", minimo: 10);
                NovoLote(5, 200, 5m);                                // 5 de um mínimo de 10: estoque baixo

                return (vencido, critico, atencao);
            }
        }

        // ---------- Um alerta de cada tipo, com a ação certa ----------

        [Fact]
        public async Task Um_alerta_de_cada_tipo_com_a_acao_o_tipo_e_os_ids_certos()
        {
            var c = new Cenario();
            var (vencido, critico, atencao) = c.UmDeCadaTipo();

            var alertas = await c.Servico.ListarAsync(null, null, null);

            Assert.Equal(5, alertas.Count);

            var venc = alertas.Single(a => a.Tipo == TipoAlerta.VENCIDO);
            Assert.Equal((AcaoAlerta.REGISTRAR_PERDA, SeveridadeAlerta.CRITICA), (venc.Acao, venc.Severidade));
            Assert.Equal((1L, vencido.Id, "Y-VENCIDO", UnidadeMedida.G), (venc.InsumoId, venc.LoteId!.Value, venc.CodigoLote, venc.UnidadeMedida));
            Assert.Equal((-2, 5m), (venc.DiasParaVencer, venc.Saldo));

            var crit = alertas.Single(a => a.Tipo == TipoAlerta.VALIDADE_CRITICA);
            Assert.Equal((AcaoAlerta.VER_LOTES, SeveridadeAlerta.ALTA), (crit.Acao, crit.Severidade));
            Assert.Equal((2L, critico.Id, 10), (crit.InsumoId, crit.LoteId!.Value, crit.DiasParaVencer));

            var aten = alertas.Single(a => a.Tipo == TipoAlerta.ATENCAO);
            Assert.Equal((AcaoAlerta.VER_LOTES, SeveridadeAlerta.MEDIA), (aten.Acao, aten.Severidade));
            Assert.Equal((3L, atencao.Id, 30), (aten.InsumoId, aten.LoteId!.Value, aten.DiasParaVencer));

            var zero = alertas.Single(a => a.Tipo == TipoAlerta.ESTOQUE_VAZIO);
            Assert.Equal((AcaoAlerta.DAR_ENTRADA, SeveridadeAlerta.ALTA), (zero.Acao, zero.Severidade));
            Assert.Equal(4L, zero.InsumoId);
            Assert.Null(zero.LoteId);       // sem lote
            Assert.Null(zero.CodigoLote);
            Assert.Null(zero.DiasParaVencer);
            Assert.Equal(0m, zero.Saldo);

            var baixo = alertas.Single(a => a.Tipo == TipoAlerta.ESTOQUE_BAIXO);
            Assert.Equal((AcaoAlerta.VER_COMPRAS, SeveridadeAlerta.BAIXA), (baixo.Acao, baixo.Severidade));
            Assert.Equal(5L, baixo.InsumoId);
            Assert.Null(baixo.LoteId);
            Assert.Equal((5m, 10m), (baixo.Saldo, baixo.EstoqueMinimo));
        }

        [Fact]
        public async Task Todo_alerta_traz_unidade_de_medida_insumo_e_mensagem()
        {
            var c = new Cenario();
            c.UmDeCadaTipo();

            var alertas = await c.Servico.ListarAsync(null, null, null);

            Assert.All(alertas, a =>
            {
                Assert.False(string.IsNullOrWhiteSpace(a.InsumoNome));
                Assert.False(string.IsNullOrWhiteSpace(a.Mensagem));
                Assert.True(a.InsumoId > 0);
            });
            Assert.Equal(UnidadeMedida.G, alertas.Single(a => a.Tipo == TipoAlerta.VENCIDO).UnidadeMedida);
        }

        [Fact]
        public void A_acao_de_cada_tipo_e_decidida_em_um_so_lugar()
        {
            Assert.Equal((SeveridadeAlerta.CRITICA, AcaoAlerta.REGISTRAR_PERDA), AlertasService.Regra(TipoAlerta.VENCIDO));
            Assert.Equal((SeveridadeAlerta.ALTA, AcaoAlerta.VER_LOTES), AlertasService.Regra(TipoAlerta.VALIDADE_CRITICA));
            Assert.Equal((SeveridadeAlerta.MEDIA, AcaoAlerta.VER_LOTES), AlertasService.Regra(TipoAlerta.ATENCAO));
            Assert.Equal((SeveridadeAlerta.ALTA, AcaoAlerta.DAR_ENTRADA), AlertasService.Regra(TipoAlerta.ESTOQUE_VAZIO));
            Assert.Equal((SeveridadeAlerta.BAIXA, AcaoAlerta.VER_COMPRAS), AlertasService.Regra(TipoAlerta.ESTOQUE_BAIXO));
            Assert.All(Enum.GetValues<TipoAlerta>(), t => AlertasService.Regra(t));   // nenhum tipo ficou sem regra
        }

        // ---------- Limites de validade do insumo ----------

        [Theory]
        [InlineData(-1, TipoAlerta.VENCIDO)]
        [InlineData(0, TipoAlerta.VALIDADE_CRITICA)]     // vence hoje ainda não venceu
        [InlineData(15, TipoAlerta.VALIDADE_CRITICA)]
        [InlineData(16, TipoAlerta.ATENCAO)]
        [InlineData(45, TipoAlerta.ATENCAO)]
        public async Task CT27_Limites_de_dias_do_insumo_definem_o_tipo_do_alerta_de_validade(int validadeEmDias, TipoAlerta esperado)
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte");
            c.NovoLote(1, validadeEmDias, 10m);
            c.NovoLote(1, 300, 10m);   // mantém saldo utilizável: só o alerta de validade aparece

            var alerta = Assert.Single(await c.Servico.ListarAsync(null, null, null));

            Assert.Equal(esperado, alerta.Tipo);
        }

        [Fact]
        public async Task CT28_Lote_com_validade_longe_nao_gera_alerta()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte");
            c.NovoLote(1, 46, 10m);

            Assert.Empty(await c.Servico.ListarAsync(null, null, null));
        }

        [Fact]
        public async Task Cada_insumo_usa_os_seus_proprios_dias_de_alerta()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Levedura", critica: 5, atencao: 10);
            c.NovoInsumo(2, "Malte", critica: 30, atencao: 90);
            c.NovoLote(1, 20, 10m);   // 20 dias: longe para a levedura
            c.NovoLote(2, 20, 10m);   // 20 dias: já é crítico para o malte

            var alerta = Assert.Single(await c.Servico.ListarAsync(null, null, null));

            Assert.Equal((2L, TipoAlerta.VALIDADE_CRITICA), (alerta.InsumoId, alerta.Tipo));
        }

        // ---------- Ordenação ----------

        [Fact]
        public async Task CT29_Urgentes_antes_dos_de_atencao_e_em_cada_grupo_menos_dias_para_vencer_primeiro()
        {
            var c = new Cenario();
            c.UmDeCadaTipo();

            var alertas = await c.Servico.ListarAsync(null, null, null);

            Assert.Equal(
                new[] { TipoAlerta.VENCIDO, TipoAlerta.VALIDADE_CRITICA, TipoAlerta.ESTOQUE_VAZIO, TipoAlerta.ATENCAO, TipoAlerta.ESTOQUE_BAIXO },
                alertas.Select(a => a.Tipo));
            Assert.Equal(
                new[] { SeveridadeAlerta.CRITICA, SeveridadeAlerta.ALTA, SeveridadeAlerta.ALTA, SeveridadeAlerta.MEDIA, SeveridadeAlerta.BAIXA },
                alertas.Select(a => a.Severidade));
        }

        [Fact]
        public async Task Dentro_do_grupo_vermelho_o_vencido_ha_mais_tempo_vem_antes()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte");
            c.NovoInsumo(2, "Lúpulo");
            c.NovoLote(1, -1, 5m, "A");
            c.NovoLote(2, -10, 5m, "B");
            c.NovoLote(1, 100, 5m);
            c.NovoLote(2, 100, 5m);

            var codigos = (await c.Servico.ListarAsync(null, null, null)).Select(a => a.CodigoLote);

            Assert.Equal(new[] { "B", "A" }, codigos);
        }

        [Fact]
        public async Task Empate_de_dias_desempata_pelo_nome_do_insumo()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte");
            c.NovoInsumo(2, "Cevada");
            c.NovoLote(1, 10, 5m);
            c.NovoLote(2, 10, 5m);

            var nomes = (await c.Servico.ListarAsync(null, null, null)).Select(a => a.InsumoNome);

            Assert.Equal(new[] { "Cevada", "Malte" }, nomes);
        }

        // ---------- Filtros ----------

        [Fact]
        public async Task Filtros_tipo_e_severidade_funcionam_juntos()
        {
            var c = new Cenario();
            c.UmDeCadaTipo();

            Assert.Equal(new[] { TipoAlerta.VENCIDO },
                (await c.Servico.ListarAsync(null, SeveridadeAlerta.CRITICA, null)).Select(a => a.Tipo));

            Assert.Equal(new[] { TipoAlerta.VALIDADE_CRITICA, TipoAlerta.ESTOQUE_VAZIO },
                (await c.Servico.ListarAsync(null, SeveridadeAlerta.ALTA, null)).Select(a => a.Tipo));

            Assert.Equal(TipoAlerta.ESTOQUE_VAZIO,
                Assert.Single(await c.Servico.ListarAsync(TipoAlerta.ESTOQUE_VAZIO, SeveridadeAlerta.ALTA, null)).Tipo);

            Assert.Equal(TipoAlerta.ESTOQUE_BAIXO,
                Assert.Single(await c.Servico.ListarAsync(TipoAlerta.ESTOQUE_BAIXO, null, null)).Tipo);

            // tipo e severidade que não combinam: nada
            Assert.Empty(await c.Servico.ListarAsync(TipoAlerta.ATENCAO, SeveridadeAlerta.ALTA, null));
            Assert.Empty(await c.Servico.ListarAsync(TipoAlerta.VENCIDO, SeveridadeAlerta.BAIXA, null));
        }

        [Fact]
        public async Task Limite_corta_a_lista_depois_de_ordenar()
        {
            var c = new Cenario();
            c.UmDeCadaTipo();

            var dois = await c.Servico.ListarAsync(null, null, 2);
            Assert.Equal(new[] { TipoAlerta.VENCIDO, TipoAlerta.VALIDADE_CRITICA }, dois.Select(a => a.Tipo));

            // com filtro: o limite vale sobre o que sobrou do filtro
            var umAmarelo = await c.Servico.ListarAsync(null, SeveridadeAlerta.MEDIA, 1);
            Assert.Equal(TipoAlerta.ATENCAO, Assert.Single(umAmarelo).Tipo);

            Assert.Equal(5, (await c.Servico.ListarAsync(null, null, 500)).Count);
            Assert.Equal(5, (await c.Servico.ListarAsync(null, null, 50)).Count);   // limite maior que o total
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        [InlineData(501)]
        public async Task Limite_invalido_gera_400(int limite)
        {
            var c = new Cenario();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Servico.ListarAsync(null, null, limite));

            Assert.True(ex.Detalhes.ContainsKey("limite"));
        }

        // ---------- Quem não gera alerta ----------

        [Fact]
        public async Task Lote_com_saldo_zero_nao_gera_alerta_de_validade()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte");
            c.NovoLote(1, -5, 0m);     // esgotado e vencido
            c.NovoLote(1, 5, 0m);      // esgotado e crítico
            c.NovoLote(1, 300, 10m);   // lote bom

            Assert.Empty(await c.Servico.ListarAsync(null, null, null));
        }

        [Fact]
        public async Task Insumo_inativo_nao_gera_alerta()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte Antigo", ativo: false);
            c.NovoLote(1, -3, 10m);

            Assert.Empty(await c.Servico.ListarAsync(null, null, null));
        }

        [Fact]
        public async Task Sem_nenhum_insumo_a_lista_vem_vazia()
        {
            Assert.Empty(await new Cenario().Servico.ListarAsync(null, null, null));
        }

        [Fact]
        public async Task CT30_Insumo_so_com_lote_vencido_gera_o_alerta_do_lote_e_o_de_estoque_zerado()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Malte");
            c.NovoLote(1, -3, 10m);

            var alertas = await c.Servico.ListarAsync(null, null, null);

            Assert.Equal(new[] { TipoAlerta.VENCIDO, TipoAlerta.ESTOQUE_VAZIO }, alertas.Select(a => a.Tipo));
        }

        [Fact]
        public async Task Estoque_exatamente_no_minimo_nao_e_baixo()
        {
            var c = new Cenario();
            c.NovoInsumo(1, "Sal", minimo: 10);
            c.NovoLote(1, 200, 10m);

            Assert.Empty(await c.Servico.ListarAsync(null, null, null));
        }

        // ---------- O alerta some depois da perda ----------

        private static PerdaService PerdaSobre(Cenario c, out PerdaRepoFalso repo)
        {
            repo = new PerdaRepoFalso(c.Lotes.Dados, c.Insumos.Dados);
            return new PerdaService(repo, new RelogioFixo());
        }

        [Fact]
        public async Task Perda_que_zera_o_lote_vencido_faz_o_alerta_sumir()
        {
            var c = new Cenario();
            var (vencido, _, _) = c.UmDeCadaTipo();
            var perdas = PerdaSobre(c, out _);
            Assert.Contains(await c.Servico.ListarAsync(null, null, null), a => a.Tipo == TipoAlerta.VENCIDO);

            await perdas.RegistrarAsync(vencido.Id, new PerdaRequest { Quantidade = 5m, Motivo = "Lote vencido, descartado" }, 7);

            var depois = await c.Servico.ListarAsync(null, null, null);
            Assert.DoesNotContain(depois, a => a.Tipo == TipoAlerta.VENCIDO);
            Assert.Equal(4, depois.Count);   // os outros quatro continuam
        }

        [Fact]
        public async Task Perda_parcial_nao_faz_o_alerta_sumir_e_atualiza_o_saldo_do_alerta()
        {
            var c = new Cenario();
            var (vencido, _, _) = c.UmDeCadaTipo();
            var perdas = PerdaSobre(c, out _);

            await perdas.RegistrarAsync(vencido.Id, new PerdaRequest { Quantidade = 2m, Motivo = "Descarte parcial" }, 7);

            var alerta = (await c.Servico.ListarAsync(TipoAlerta.VENCIDO, null, null)).Single();
            Assert.Equal(3m, alerta.Saldo);
        }
    }
}
