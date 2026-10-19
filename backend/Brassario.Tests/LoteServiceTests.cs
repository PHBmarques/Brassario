using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;


namespace Brassario.Tests
{
    public class LoteServiceTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 19);

        // ---------- Fakes em memória (sem banco) ----------

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => LoteServiceTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        }

        private class InsumoRepoFalso : IInsumoRepository
        {
            public List<Insumo> Dados { get; } = new();
            public Task<List<Insumo>> ListarAsync(CategoriaInsumo? c, string? b, bool i, CancellationToken ct = default) => Task.FromResult(Dados);
            public Task<Insumo?> ObterPorIdAsync(long id, CancellationToken ct = default) => Task.FromResult(Dados.FirstOrDefault(x => x.Id == id));
            public Task<bool> ExisteNomeAsync(string n, long? ig, CancellationToken ct = default) => Task.FromResult(false);
            public Task AdicionarAsync(Insumo i, CancellationToken ct = default) { Dados.Add(i); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private class FornecedorRepoFalso : IFornecedorRepository
        {
            public List<Fornecedor> Dados { get; } = new();
            public Task<Fornecedor?> ObterPorIdAsync(long id, CancellationToken ct = default) => Task.FromResult(Dados.FirstOrDefault(x => x.Id == id));
        }

        private class LoteRepoFalso : ILoteRepository
        {
            public List<LoteInsumo> Dados { get; } = new();
            public int Salvamentos { get; private set; }

            public Task<bool> ExisteCodigoAsync(long insumoId, string codigo, CancellationToken ct = default) =>
                Task.FromResult(Dados.Any(l => l.InsumoId == insumoId &&
                    string.Equals(l.CodigoLote, codigo.Trim(), StringComparison.OrdinalIgnoreCase)));

            public Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default)
            {
                lote.Id = Dados.Count + 1;
                Dados.Add(lote);
                return Task.CompletedTask;
            }

            public Task SalvarAsync(CancellationToken ct = default) { Salvamentos++; return Task.CompletedTask; }

            public Task<List<LoteInsumo>> ListarPorInsumosAsync(IReadOnlyCollection<long> insumoIds, bool incluirEsgotados, CancellationToken ct = default) =>
                Task.FromResult(Dados.Where(l => insumoIds.Contains(l.InsumoId) && (incluirEsgotados || l.QuantidadeAtual > 0)).ToList());
        }

        private class Cenario
        {
            public InsumoRepoFalso Insumos { get; } = new();
            public FornecedorRepoFalso Fornecedores { get; } = new();
            public LoteRepoFalso Lotes { get; } = new();
            public LoteService Servico { get; }

            public Cenario()
            {
                Insumos.Dados.Add(new Insumo
                {
                    Id = 1,
                    Nome = "Malte Pilsen",
                    Categoria = CategoriaInsumo.MALTE,
                    UnidadeMedida = UnidadeMedida.KG,
                    DiasValidadeCritica = 15,
                    DiasValidadeAtencao = 45,
                    Ativo = true
                });
                Fornecedores.Dados.Add(new Fornecedor { Id = 1, Nome = "Maltaria Sul", Ativo = true });
                Servico = new LoteService(Lotes, Insumos, Fornecedores, new RelogioFixo());
            }
        }

        private static LoteRequest Valido(int diasParaVencer = 120, string codigo = "L-001") => new()
        {
            InsumoId = 1,
            FornecedorId = 1,
            CodigoLote = codigo,
            DataValidade = Hoje.AddDays(diasParaVencer),
            Quantidade = 25,
            PrecoUnitario = 8.5m
        };

        // ---------- Casos ----------

        [Fact]
        public async Task Cria_lote_com_saldo_atual_igual_ao_inicial()
        {
            var c = new Cenario();
            var resposta = await c.Servico.RegistrarEntradaAsync(Valido(), usuarioId: 7);

            Assert.Equal(25, resposta.QuantidadeInicial);
            Assert.Equal(25, resposta.QuantidadeAtual);
            Assert.Equal(Hoje, resposta.DataEntrada);
            Assert.Equal("Malte Pilsen", resposta.InsumoNome);
            Assert.Equal("Maltaria Sul", resposta.FornecedorNome);
            Assert.Single(c.Lotes.Dados);
        }

        [Fact]
        public async Task Grava_movimentacao_ENTRADA_do_usuario_logado_junto_com_o_lote()
        {
            var c = new Cenario();
            await c.Servico.RegistrarEntradaAsync(Valido(), usuarioId: 7);

            var lote = Assert.Single(c.Lotes.Dados);
            var mov = Assert.Single(lote.Movimentacoes);
            Assert.Equal(TipoMovimentacao.ENTRADA, mov.Tipo);
            Assert.Equal(7, mov.UsuarioId);
            Assert.Equal(25, mov.Quantidade);
            Assert.Equal(1, c.Lotes.Salvamentos); // um único salvamento = uma única transação
        }

        [Fact]
        public async Task Validade_vencida_vira_regra_de_negocio_422()
        {
            var c = new Cenario();
            var ex = await Assert.ThrowsAsync<NegocioException>(
                () => c.Servico.RegistrarEntradaAsync(Valido(diasParaVencer: -1), 7));

            Assert.Equal("VALIDADE_VENCIDA", ex.Codigo);
            Assert.Empty(c.Lotes.Dados);
        }

        [Fact]
        public async Task Validade_de_hoje_ainda_e_aceita()
        {
            var c = new Cenario();
            var resposta = await c.Servico.RegistrarEntradaAsync(Valido(diasParaVencer: 0), 7);
            Assert.Equal(SituacaoValidade.VERMELHO, resposta.SituacaoValidade);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Quantidade_zero_ou_negativa_vira_validacao_400(int quantidade)
        {
            var c = new Cenario();
            var request = Valido();
            request.Quantidade = quantidade;

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Servico.RegistrarEntradaAsync(request, 7));
            Assert.Contains("quantidade", ex.Detalhes.Keys);
            Assert.Empty(c.Lotes.Dados);
        }

        [Fact]
        public async Task Codigo_de_lote_repetido_para_o_mesmo_insumo_vira_conflito_409()
        {
            var c = new Cenario();
            await c.Servico.RegistrarEntradaAsync(Valido(codigo: "L-001"), 7);

            await Assert.ThrowsAsync<ConflitoException>(
                () => c.Servico.RegistrarEntradaAsync(Valido(codigo: " l-001 "), 7));
            Assert.Single(c.Lotes.Dados);
        }

        [Fact]
        public async Task Mesmo_codigo_em_outro_insumo_e_permitido()
        {
            var c = new Cenario();
            c.Insumos.Dados.Add(new Insumo { Id = 2, Nome = "Cascade", Categoria = CategoriaInsumo.LUPULO, UnidadeMedida = UnidadeMedida.G, Ativo = true });
            await c.Servico.RegistrarEntradaAsync(Valido(codigo: "L-001"), 7);

            var outro = Valido(codigo: "L-001");
            outro.InsumoId = 2;
            await c.Servico.RegistrarEntradaAsync(outro, 7);

            Assert.Equal(2, c.Lotes.Dados.Count);
        }

        [Fact]
        public async Task Insumo_inexistente_vira_404()
        {
            var c = new Cenario();
            var request = Valido();
            request.InsumoId = 999;

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => c.Servico.RegistrarEntradaAsync(request, 7));
        }

        [Fact]
        public async Task Fornecedor_inexistente_vira_404()
        {
            var c = new Cenario();
            var request = Valido();
            request.FornecedorId = 999;

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => c.Servico.RegistrarEntradaAsync(request, 7));
        }

        [Fact]
        public async Task Fornecedor_e_opcional()
        {
            var c = new Cenario();
            var request = Valido();
            request.FornecedorId = null;

            var resposta = await c.Servico.RegistrarEntradaAsync(request, 7);
            Assert.Null(resposta.FornecedorId);
        }

        [Fact]
        public async Task Insumo_inativo_nao_recebe_entrada()
        {
            var c = new Cenario();
            c.Insumos.Dados[0].Ativo = false;

            var ex = await Assert.ThrowsAsync<NegocioException>(() => c.Servico.RegistrarEntradaAsync(Valido(), 7));
            Assert.Equal("INSUMO_INATIVO", ex.Codigo);
        }

        [Fact]
        public async Task Data_de_entrada_futura_e_recusada()
        {
            var c = new Cenario();
            var request = Valido();
            request.DataEntrada = Hoje.AddDays(3);

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Servico.RegistrarEntradaAsync(request, 7));
            Assert.Contains("dataEntrada", ex.Detalhes.Keys);
        }

        [Theory]
        [InlineData(10, SituacaoValidade.VERMELHO)]   // até 15 dias: crítica
        [InlineData(15, SituacaoValidade.VERMELHO)]
        [InlineData(16, SituacaoValidade.AMARELO)]    // até 45 dias: atenção
        [InlineData(45, SituacaoValidade.AMARELO)]
        [InlineData(46, SituacaoValidade.VERDE)]
        [InlineData(200, SituacaoValidade.VERDE)]
        public async Task Resposta_traz_a_situacao_da_validade(int dias, SituacaoValidade esperada)
        {
            var c = new Cenario();
            var resposta = await c.Servico.RegistrarEntradaAsync(Valido(diasParaVencer: dias), 7);

            Assert.Equal(esperada, resposta.SituacaoValidade);
            Assert.Equal(dias, resposta.DiasParaVencer);
        }

        [Fact]
        public void Calculador_usa_os_limites_de_cada_insumo()
        {
            // Um insumo com limites próprios (levedura: crítica 5, atenção 10)
            Assert.Equal(SituacaoValidade.AMARELO, SituacaoValidadeCalculador.Calcular(Hoje.AddDays(8), Hoje, 5, 10));
            Assert.Equal(SituacaoValidade.VERMELHO, SituacaoValidadeCalculador.Calcular(Hoje.AddDays(3), Hoje, 5, 10));
            Assert.Equal(SituacaoValidade.VERDE, SituacaoValidadeCalculador.Calcular(Hoje.AddDays(30), Hoje, 5, 10));
            Assert.Equal(SituacaoValidade.VERMELHO, SituacaoValidadeCalculador.Calcular(Hoje.AddDays(-2), Hoje, 5, 10)); // já vencido
        }
    }
}
