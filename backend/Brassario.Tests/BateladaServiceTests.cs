using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    public class BateladaServiceTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 20);
        private const long Usuario = 7;

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => BateladaServiceTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        }

        private class ReceitasStub : IReceitaService
        {
            public Receita? Receita { get; set; }
            public Exception? Erro { get; set; }

            public Task<Receita> ObterParaProducaoAsync(long id, CancellationToken ct = default)
            {
                if (Erro is not null) throw Erro;
                return Task.FromResult(Receita!);
            }

            public Task<List<ReceitaResumoResponse>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> ObterAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> CriarAsync(ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AtualizarAsync(long id, ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AlterarStatusAsync(long id, bool ativa, CancellationToken ct = default) => throw new NotSupportedException();
        }

        // Repositório em memória que imita a transação: se a operação lançar exceção, restaura os saldos (rollback)
        private class BateladasFalso : IBateladaRepository
        {
            public List<LoteInsumo> Lotes { get; } = new();
            public List<Batelada> Gravadas { get; } = new();
            private readonly List<Batelada> _pendentes = new();
            public int Salvamentos { get; private set; }
            public int Bloqueios { get; private set; }
            public bool FalharAoSalvar { get; set; }

            public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default)
            {
                var copia = Lotes.Select(l => (l, l.QuantidadeAtual, l.Versao)).ToList();
                try
                {
                    return await operacao();
                }
                catch
                {
                    foreach (var (lote, qtd, versao) in copia)
                    {
                        lote.QuantidadeAtual = qtd;
                        lote.Versao = versao;
                    }
                    _pendentes.Clear();
                    throw;
                }
            }

            public Task<List<LoteInsumo>> BloquearLotesAsync(IReadOnlyCollection<long> insumoIds, CancellationToken ct = default)
            {
                Bloqueios++;
                return Task.FromResult(Lotes
                    .Where(l => insumoIds.Contains(l.InsumoId) && l.QuantidadeAtual > 0)
                    .OrderBy(l => l.InsumoId).ThenBy(l => l.DataValidade).ThenBy(l => l.DataEntrada).ThenBy(l => l.Id)
                    .ToList());
            }

            public Task<string> ProximoCodigoAsync(int ano, CancellationToken ct = default) =>
                Task.FromResult($"B-{ano}-{Gravadas.Count + 1:0000}");

            public Task AdicionarAsync(Batelada batelada, CancellationToken ct = default)
            {
                _pendentes.Add(batelada);
                return Task.CompletedTask;
            }

            public Task SalvarAsync(CancellationToken ct = default)
            {
                if (FalharAoSalvar) throw new InvalidOperationException("Falha simulada ao gravar.");
                Salvamentos++;
                foreach (var b in _pendentes)
                {
                    b.Id = Gravadas.Count + 1;
                    Gravadas.Add(b);
                }
                _pendentes.Clear();
                return Task.CompletedTask;
            }
        }

        private static (BateladaService Servico, ReceitasStub Receitas, BateladasFalso Repo) Criar(bool ativa = true)
        {
            var receitas = new ReceitasStub { Receita = NovaReceita(ativa) };
            var repo = new BateladasFalso();
            return (new BateladaService(receitas, repo, new RelogioFixo()), receitas, repo);
        }

        // IPA Cascade: base 100 L, 18 kg de Malte Pilsen (1) e 0,3 kg de Lúpulo Cascade (2)
        private static Receita NovaReceita(bool ativa = true)
        {
            var malte = new Insumo { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG };
            var lupulo = new Insumo { Id = 2, Nome = "Lúpulo Cascade", UnidadeMedida = UnidadeMedida.KG };
            var receita = new Receita { Id = 3, Nome = "IPA Cascade", VolumeBaseLitros = 100, Ativa = ativa };
            receita.Itens.Add(new ReceitaItem { InsumoId = 1, Insumo = malte, Quantidade = 18m });
            receita.Itens.Add(new ReceitaItem { InsumoId = 2, Insumo = lupulo, Quantidade = 0.3m });
            return receita;
        }

        private static LoteInsumo Lote(long id, long insumoId, string codigo, DateOnly validade, decimal saldo, decimal preco, DateOnly? entrada = null) => new()
        {
            Id = id,
            InsumoId = insumoId,
            CodigoLote = codigo,
            DataEntrada = entrada ?? Hoje.AddDays(-30),
            DataValidade = validade,
            QuantidadeInicial = saldo,
            QuantidadeAtual = saldo,
            PrecoUnitario = preco
        };

        private static BateladaRequest Pedido(decimal? volume = 100, long receitaId = 3, string? obs = null) =>
            new() { ReceitaId = receitaId, VolumePlanejado = volume, Observacoes = obs };

        // Estoque padrão: malte em dois lotes (10 + 40) e lúpulo em um (1)
        private static void EstoqueCompleto(BateladasFalso repo)
        {
            repo.Lotes.Add(Lote(1, 1, "M-TARDE", Hoje.AddDays(200), 40m, 9.2m));
            repo.Lotes.Add(Lote(2, 1, "M-CEDO", Hoje.AddDays(30), 10m, 8.5m));
            repo.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(90), 1m, 120m));
        }

        private static decimal Saldo(BateladasFalso repo, long loteId) => repo.Lotes.Single(l => l.Id == loteId).QuantidadeAtual;

        // ---------- Produção com sucesso ----------

        [Fact]
        public async Task Produz_e_abate_o_estoque_por_FEFO()
        {
            var (servico, _, repo) = Criar();
            EstoqueCompleto(repo);

            var r = await servico.ProduzirAsync(Pedido(100), Usuario);

            Assert.Equal(0m, Saldo(repo, 2));     // M-CEDO esvaziou primeiro
            Assert.Equal(32m, Saldo(repo, 1));    // M-TARDE deu os 8 que faltavam
            Assert.Equal(0.7m, Saldo(repo, 3));   // lúpulo: 1 - 0,3
            Assert.Equal(1, repo.Salvamentos);

            var malte = r.Consumos.Where(c => c.InsumoId == 1).ToList();
            Assert.Equal(2, malte.Count);
            Assert.Equal(("M-CEDO", 10m), (malte[0].CodigoLote, malte[0].Quantidade));
            Assert.Equal(("M-TARDE", 8m), (malte[1].CodigoLote, malte[1].Quantidade));
        }

        [Fact]
        public async Task Cria_batelada_consumos_e_movimentacoes_de_saida()
        {
            var (servico, _, repo) = Criar();
            EstoqueCompleto(repo);

            var r = await servico.ProduzirAsync(Pedido(100, obs: "  Primeira IPA  "), Usuario);

            var batelada = Assert.Single(repo.Gravadas);
            Assert.Equal(StatusBatelada.EM_ANDAMENTO, batelada.Status);
            Assert.Equal(3, batelada.ReceitaId);
            Assert.Equal(Usuario, batelada.UsuarioId);
            Assert.Equal(100m, batelada.VolumePlanejado);
            Assert.Equal("Primeira IPA", batelada.Observacoes);
            Assert.Equal(3, batelada.Consumos.Count);

            Assert.Equal(3, batelada.Movimentacoes.Count);
            Assert.All(batelada.Movimentacoes, m =>
            {
                Assert.Equal(TipoMovimentacao.SAIDA_PRODUCAO, m.Tipo);
                Assert.True(m.Quantidade < 0);          // saída é negativa
                Assert.Equal(Usuario, m.UsuarioId);
            });

            // cada movimentação bate com o consumo do mesmo lote
            foreach (var c in batelada.Consumos)
                Assert.Equal(-c.Quantidade, batelada.Movimentacoes.Single(m => m.LoteInsumo == c.LoteInsumo).Quantidade);

            Assert.Equal(r.Id, batelada.Id);
            Assert.Equal("IPA Cascade", r.Receita);
        }

        [Fact]
        public async Task Incrementa_a_versao_dos_lotes_alterados_e_so_deles()
        {
            var (servico, _, repo) = Criar();
            EstoqueCompleto(repo);
            repo.Lotes.Add(Lote(9, 1, "M-INTOCADO", Hoje.AddDays(900), 5m, 9m));   // não é necessário: sobra

            await servico.ProduzirAsync(Pedido(100), Usuario);

            Assert.Equal(1, repo.Lotes.Single(l => l.Id == 1).Versao);
            Assert.Equal(1, repo.Lotes.Single(l => l.Id == 2).Versao);
            Assert.Equal(0, repo.Lotes.Single(l => l.Id == 9).Versao);
            Assert.Equal(5m, Saldo(repo, 9));
        }

        [Fact]
        public async Task Calcula_o_custo_dos_consumos_e_o_custo_total()
        {
            var (servico, _, repo) = Criar();
            EstoqueCompleto(repo);

            var r = await servico.ProduzirAsync(Pedido(100), Usuario);

            // 10 x 8,5 + 8 x 9,2 + 0,3 x 120 = 85 + 73,6 + 36
            Assert.Equal(85m, r.Consumos.Single(c => c.CodigoLote == "M-CEDO").Custo);
            Assert.Equal(73.6m, r.Consumos.Single(c => c.CodigoLote == "M-TARDE").Custo);
            Assert.Equal(36m, r.Consumos.Single(c => c.CodigoLote == "L-1").Custo);
            Assert.Equal(194.6m, r.CustoTotal);
        }

        [Fact]
        public async Task Escala_pelo_volume_planejado()
        {
            var (servico, _, repo) = Criar();
            EstoqueCompleto(repo);

            var r = await servico.ProduzirAsync(Pedido(50), Usuario);

            Assert.Equal(9m, r.Consumos.Where(c => c.InsumoId == 1).Sum(c => c.Quantidade));
            Assert.Equal(0.15m, r.Consumos.Single(c => c.InsumoId == 2).Quantidade);
            Assert.Equal(50m, r.VolumePlanejado);
        }

        [Fact]
        public async Task Gera_codigo_sequencial_do_ano()
        {
            var (servico, _, repo) = Criar();
            EstoqueCompleto(repo);

            var primeira = await servico.ProduzirAsync(Pedido(10), Usuario);
            var segunda = await servico.ProduzirAsync(Pedido(10), Usuario);

            Assert.Equal("B-2026-0001", primeira.Codigo);
            Assert.Equal("B-2026-0002", segunda.Codigo);
        }

        [Fact]
        public async Task Lote_vencido_nao_e_consumido()
        {
            var (servico, _, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-VENCIDO", Hoje.AddDays(-1), 100m, 5m));
            repo.Lotes.Add(Lote(2, 1, "M-OK", Hoje.AddDays(60), 20m, 9m));
            repo.Lotes.Add(Lote(3, 2, "L-1", Hoje.AddDays(90), 1m, 120m));

            var r = await servico.ProduzirAsync(Pedido(100), Usuario);

            Assert.Equal(100m, Saldo(repo, 1));   // vencido intacto
            Assert.DoesNotContain(r.Consumos, c => c.CodigoLote == "M-VENCIDO");
            Assert.Equal(2m, Saldo(repo, 2));
        }

        [Fact]
        public async Task Lote_que_vence_hoje_ainda_e_consumido()
        {
            var (servico, _, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-HOJE", Hoje, 30m, 9m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje, 1m, 120m));

            var r = await servico.ProduzirAsync(Pedido(100), Usuario);

            Assert.Contains(r.Consumos, c => c.CodigoLote == "M-HOJE");
        }

        [Fact]
        public async Task Trava_os_lotes_uma_vez_por_producao()
        {
            var (servico, _, repo) = Criar();
            EstoqueCompleto(repo);

            await servico.ProduzirAsync(Pedido(100), Usuario);

            Assert.Equal(1, repo.Bloqueios);
        }

        // ---------- Estoque insuficiente: tudo ou nada ----------

        [Fact]
        public async Task Estoque_insuficiente_gera_422_com_a_lista_de_faltas_e_nada_e_abatido()
        {
            var (servico, _, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 5m, 9m));       // precisa de 18
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 0.12m, 120m));  // precisa de 0,3

            var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(100), Usuario));

            Assert.Equal("ESTOQUE_INSUFICIENTE", ex.Codigo);
            Assert.NotNull(ex.Detalhes);
            Assert.Equal(2, ex.Detalhes!.Count);                 // as DUAS faltas, não só a primeira
            Assert.True(ex.Detalhes.ContainsKey("Malte Pilsen"));
            Assert.True(ex.Detalhes.ContainsKey("Lúpulo Cascade"));

            Assert.Equal(5m, Saldo(repo, 1));
            Assert.Equal(0.12m, Saldo(repo, 2));
            Assert.Empty(repo.Gravadas);
            Assert.Equal(0, repo.Salvamentos);
        }

        [Fact]
        public async Task Falta_de_um_insumo_nao_abate_os_que_tinham_estoque()
        {
            var (servico, _, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-1", Hoje.AddDays(100), 50m, 9m));       // malte de sobra
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 0.12m, 120m));   // lúpulo falta

            var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(100), Usuario));

            Assert.Single(ex.Detalhes!);
            Assert.Equal(50m, Saldo(repo, 1));    // malte intacto
            Assert.Equal(0, repo.Lotes.Single(l => l.Id == 1).Versao);
            Assert.Empty(repo.Gravadas);
        }

        [Fact]
        public async Task Insumo_so_com_lote_vencido_conta_como_falta()
        {
            var (servico, _, repo) = Criar();
            repo.Lotes.Add(Lote(1, 1, "M-VENCIDO", Hoje.AddDays(-3), 100m, 9m));
            repo.Lotes.Add(Lote(2, 2, "L-1", Hoje.AddDays(100), 1m, 120m));

            var ex = await Assert.ThrowsAsync<EstoqueInsuficienteException>(() => servico.ProduzirAsync(Pedido(100), Usuario));

            Assert.True(ex.Detalhes!.ContainsKey("Malte Pilsen"));
            Assert.Equal(100m, Saldo(repo, 1));
        }

        [Fact]
        public async Task Falha_ao_gravar_desfaz_a_baixa_do_estoque()
        {
            var (servico, _, repo) = Criar();
            EstoqueCompleto(repo);
            repo.FalharAoSalvar = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => servico.ProduzirAsync(Pedido(100), Usuario));

            Assert.Equal(10m, Saldo(repo, 2));    // rollback: tudo voltou como estava
            Assert.Equal(40m, Saldo(repo, 1));
            Assert.Equal(1m, Saldo(repo, 3));
            Assert.Empty(repo.Gravadas);
        }

        // ---------- Receita e validações ----------

        [Fact]
        public async Task Receita_inexistente_gera_404_sem_travar_lotes()
        {
            var (servico, receitas, repo) = Criar();
            receitas.Erro = new RecursoNaoEncontradoException("Receita", 99);

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ProduzirAsync(Pedido(100, 99), Usuario));
            Assert.Equal(0, repo.Bloqueios);
        }

        [Fact]
        public async Task Receita_inativa_gera_422_status_invalido()
        {
            var (servico, receitas, repo) = Criar();
            receitas.Erro = new NegocioException("A receita está inativa e não pode ser produzida.", "STATUS_INVALIDO");

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.ProduzirAsync(Pedido(100), Usuario));

            Assert.Equal("STATUS_INVALIDO", ex.Codigo);
            Assert.Equal(0, repo.Bloqueios);
        }

        [Theory]
        [InlineData(null)]
        [InlineData(0.0)]
        [InlineData(-5.0)]
        [InlineData(0.001)]
        public async Task Volume_invalido_gera_400(double? volume)
        {
            var (servico, _, _) = Criar();
            decimal? pedido = volume.HasValue ? (decimal)volume.Value : null;

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ProduzirAsync(Pedido(pedido), Usuario));
            Assert.True(ex.Detalhes.ContainsKey("volumePlanejado"));
        }

        [Fact]
        public async Task Receita_nao_informada_gera_400()
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ProduzirAsync(Pedido(100, 0), Usuario));
            Assert.True(ex.Detalhes.ContainsKey("receitaId"));
        }

        [Fact]
        public async Task Observacao_com_mais_de_255_caracteres_gera_400()
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ProduzirAsync(Pedido(100, obs: new string('x', 256)), Usuario));
            Assert.True(ex.Detalhes.ContainsKey("observacoes"));
        }

        [Fact]
        public async Task Reune_todos_os_erros_de_validacao_de_uma_vez()
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ProduzirAsync(Pedido(0, 0), Usuario));
            Assert.Equal(2, ex.Detalhes.Count);
        }
    }
}
