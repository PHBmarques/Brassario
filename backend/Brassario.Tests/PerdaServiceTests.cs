using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-23: POST /lotes/{id}/perdas (RN-15).</summary>
    public class PerdaServiceTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 24);
        private const long Usuario = 7;

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => PerdaServiceTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        }

        // Repositório em memória que imita a transação: se a operação falhar, restaura saldo e versão
        private class PerdaRepoFalso : IPerdaRepository
        {
            public List<LoteInsumo> Lotes { get; } = new();
            public List<Insumo> Insumos { get; } = new();
            public List<MovimentacaoEstoque> Gravadas { get; } = new();
            private readonly List<MovimentacaoEstoque> _pendentes = new();
            public bool FalharAoSalvar { get; set; }

            public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default)
            {
                var copia = Lotes.Select(l => (l, l.QuantidadeAtual, l.Versao)).ToList();
                try { return await operacao(); }
                catch
                {
                    foreach (var (lote, saldo, versao) in copia) { lote.QuantidadeAtual = saldo; lote.Versao = versao; }
                    _pendentes.Clear();
                    throw;
                }
            }

            public Task<LoteInsumo?> BloquearLoteAsync(long loteId, CancellationToken ct = default) => Task.FromResult(Lotes.FirstOrDefault(l => l.Id == loteId));
            public Task<Insumo?> ObterInsumoAsync(long insumoId, CancellationToken ct = default) => Task.FromResult(Insumos.FirstOrDefault(i => i.Id == insumoId));
            public Task AdicionarMovimentacaoAsync(MovimentacaoEstoque movimentacao, CancellationToken ct = default) { _pendentes.Add(movimentacao); return Task.CompletedTask; }

            public Task SalvarAsync(CancellationToken ct = default)
            {
                if (FalharAoSalvar) throw new InvalidOperationException("Falha simulada ao gravar.");
                foreach (var m in _pendentes) { m.Id = Gravadas.Count + 1; Gravadas.Add(m); }
                _pendentes.Clear();
                return Task.CompletedTask;
            }
        }

        private static (PerdaService Servico, PerdaRepoFalso Repo) Criar(decimal saldo = 10m)
        {
            var repo = new PerdaRepoFalso();
            repo.Insumos.Add(new Insumo { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG });
            repo.Lotes.Add(new LoteInsumo
            {
                Id = 5, InsumoId = 1, CodigoLote = "M-1", DataEntrada = Hoje.AddDays(-30), DataValidade = Hoje.AddDays(-1),
                QuantidadeInicial = 10m, QuantidadeAtual = saldo, PrecoUnitario = 8m, Versao = 2
            });
            return (new PerdaService(repo, new RelogioFixo()), repo);
        }

        private static PerdaRequest Pedido(decimal? quantidade = 4m, string? motivo = "Lote vencido") => new() { Quantidade = quantidade, Motivo = motivo };

        // ---------- Sucesso ----------

        [Fact]
        public async Task Perda_parcial_baixa_o_saldo_incrementa_a_versao_e_grava_a_movimentacao_PERDA_negativa()
        {
            var (servico, repo) = Criar(10m);

            var r = await servico.RegistrarAsync(5, Pedido(4m, "  Embalagem rasgada  "), Usuario);

            Assert.Equal(6m, repo.Lotes[0].QuantidadeAtual);
            Assert.Equal(3, repo.Lotes[0].Versao);

            var mov = Assert.Single(repo.Gravadas);
            Assert.Equal(TipoMovimentacao.PERDA, mov.Tipo);
            Assert.Equal(-4m, mov.Quantidade);
            Assert.Equal("Embalagem rasgada", mov.Motivo);   // sem os espaços das pontas
            Assert.Equal((5L, Usuario), (mov.LoteInsumoId, mov.UsuarioId));
            Assert.Null(mov.BateladaId);
            Assert.Equal(new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc), mov.DataHora);

            Assert.Equal((5L, "M-1", 1L, "Malte Pilsen", UnidadeMedida.KG), (r.LoteId, r.CodigoLote, r.InsumoId, r.Insumo, r.UnidadeMedida));
            Assert.Equal((4m, 10m, 6m, "Embalagem rasgada"), (r.Quantidade, r.SaldoAnterior, r.SaldoAtual, r.Motivo));
        }

        [Fact]
        public async Task Perda_do_saldo_inteiro_zera_o_lote()
        {
            var (servico, repo) = Criar(10m);

            var r = await servico.RegistrarAsync(5, Pedido(10m), Usuario);

            Assert.Equal(0m, repo.Lotes[0].QuantidadeAtual);
            Assert.Equal(0m, r.SaldoAtual);
        }

        [Fact]
        public async Task Duas_perdas_seguidas_somam_ate_o_saldo()
        {
            var (servico, repo) = Criar(10m);

            await servico.RegistrarAsync(5, Pedido(6m), Usuario);
            await servico.RegistrarAsync(5, Pedido(4m), Usuario);

            Assert.Equal(0m, repo.Lotes[0].QuantidadeAtual);
            Assert.Equal(2, repo.Gravadas.Count);
            await Assert.ThrowsAsync<NegocioException>(() => servico.RegistrarAsync(5, Pedido(0.001m), Usuario));   // lote já zerado
        }

        // ---------- Erros ----------

        [Fact]
        public async Task Perda_maior_que_o_saldo_devolve_422_e_nada_muda()
        {
            var (servico, repo) = Criar(10m);

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.RegistrarAsync(5, Pedido(10.001m), Usuario));

            Assert.Equal("PERDA_MAIOR_QUE_SALDO", ex.Codigo);
            Assert.True(ex.Detalhes!.ContainsKey("quantidade"));
            Assert.Equal((10m, 2L), (repo.Lotes[0].QuantidadeAtual, repo.Lotes[0].Versao));
            Assert.Empty(repo.Gravadas);
        }

        [Fact]
        public async Task Lote_inexistente_devolve_404()
        {
            var (servico, _) = Criar();

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.RegistrarAsync(999, Pedido(), Usuario));
        }

        [Theory]
        [InlineData("")]
        [InlineData("    ")]
        [InlineData("abc")]
        [InlineData("  ab  ")]     // 2 caracteres depois do trim
        [InlineData("1234")]       // 4: um a menos que o mínimo
        public async Task Motivo_com_menos_de_5_caracteres_gera_400(string motivo)
        {
            var (servico, repo) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.RegistrarAsync(5, Pedido(motivo: motivo), Usuario));

            Assert.True(ex.Detalhes.ContainsKey("motivo"));
            Assert.Empty(repo.Gravadas);
            Assert.Equal(10m, repo.Lotes[0].QuantidadeAtual);
        }

        [Fact]
        public async Task Motivo_ausente_gera_400()
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.RegistrarAsync(5, Pedido(motivo: null), Usuario));

            Assert.True(ex.Detalhes.ContainsKey("motivo"));
        }

        [Fact]
        public async Task Motivo_com_exatamente_5_caracteres_e_aceito_e_com_mais_de_255_nao()
        {
            var (servico, _) = Criar();

            await servico.RegistrarAsync(5, Pedido(1m, "12345"), Usuario);

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.RegistrarAsync(5, Pedido(1m, new string('x', 256)), Usuario));
            Assert.True(ex.Detalhes.ContainsKey("motivo"));
            await servico.RegistrarAsync(5, Pedido(1m, new string('x', 255)), Usuario);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(1.2345)]          // 4 casas decimais
        [InlineData(1000000000.0)]    // maior que o campo aceita
        public async Task Quantidade_ausente_zero_negativa_com_casas_demais_ou_enorme_gera_400(double? quantidade)
        {
            var (servico, repo) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.RegistrarAsync(5, Pedido(quantidade.HasValue ? (decimal)quantidade.Value : null), Usuario));

            Assert.True(ex.Detalhes.ContainsKey("quantidade"));
            Assert.Empty(repo.Gravadas);
        }

        [Fact]
        public async Task Quantidade_e_motivo_invalidos_voltam_juntos_no_mesmo_400()
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.RegistrarAsync(5, Pedido(0m, "x"), Usuario));

            Assert.Equal(2, ex.Detalhes.Count);
        }

        [Fact]
        public async Task Falha_ao_gravar_desfaz_a_baixa_do_saldo()
        {
            var (servico, repo) = Criar(10m);
            repo.FalharAoSalvar = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarAsync(5, Pedido(4m), Usuario));

            Assert.Equal((10m, 2L), (repo.Lotes[0].QuantidadeAtual, repo.Lotes[0].Versao));
            Assert.Empty(repo.Gravadas);
        }
    }
}
