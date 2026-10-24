using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    public class InsumoServiceTests
    {
        private class RepositorioFalso : IInsumoRepository
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

            public Task<Insumo?> ObterPorIdAsync(long id, CancellationToken ct = default) =>
                Task.FromResult(Dados.FirstOrDefault(i => i.Id == id));

            public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default) =>
                Task.FromResult(Dados.Any(i =>
                    string.Equals(i.Nome, nome.Trim(), StringComparison.OrdinalIgnoreCase) && i.Id != ignorarId));

            public Task AdicionarAsync(Insumo insumo, CancellationToken ct = default)
            {
                insumo.Id = Dados.Count + 1;
                Dados.Add(insumo);
                return Task.CompletedTask;
            }

            public Task SalvarAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private static readonly DateOnly Hoje = new(2026, 10, 19);

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => InsumoServiceTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
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

        private static (InsumoService Servico, RepositorioFalso Repo) Criar()
        {
            var (servico, repo, _) = CriarComLotes();
            return (servico, repo);
        }

        private static (InsumoService Servico, RepositorioFalso Repo, LoteRepoFalso Lotes) CriarComLotes()
        {
            var repo = new RepositorioFalso();
            var lotes = new LoteRepoFalso();
            return (new InsumoService(repo, lotes, new RelogioFixo()), repo, lotes);
        }

        // ---------- API-17: busca para a caixa de escolha ----------

        private static Insumo NovoInsumo(long id, string nome, UnidadeMedida unidade = UnidadeMedida.KG, bool ativo = true) => new()
        {
            Id = id,
            Nome = nome,
            Categoria = CategoriaInsumo.OUTRO,
            UnidadeMedida = unidade,
            Ativo = ativo
        };

        private static LoteInsumo NovoLote(long insumoId, decimal saldo, int validadeEmDias = 60) => new()
        {
            InsumoId = insumoId,
            CodigoLote = $"L-{insumoId}-{saldo}-{validadeEmDias}",
            DataEntrada = Hoje.AddDays(-10),
            DataValidade = Hoje.AddDays(validadeEmDias),
            QuantidadeInicial = 100,
            QuantidadeAtual = saldo
        };

        [Theory]
        [InlineData("Lúp")]
        [InlineData("lup")]
        [InlineData("LÚPULO")]
        [InlineData("  lupulo  ")]
        public async Task Busca_ignora_acento_maiuscula_e_espacos_nas_pontas(string digitado)
        {
            var (servico, repo, _) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Lúpulo Cascade", UnidadeMedida.G));
            repo.Dados.Add(NovoInsumo(2, "Lúpulo Citra", UnidadeMedida.G));
            repo.Dados.Add(NovoInsumo(3, "Malte Pilsen"));

            var resultado = await servico.BuscarAsync(digitado, incluirInativos: false);

            Assert.Equal(new[] { "Lúpulo Cascade", "Lúpulo Citra" }, resultado.Select(r => r.Nome));
        }

        [Theory]
        [InlineData("acucar")]
        [InlineData("AÇÚCAR")]
        [InlineData("açucar")]
        public async Task Busca_sem_acento_acha_nome_com_acento_e_vice_versa(string digitado)
        {
            var (servico, repo, _) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Açúcar Mascavo"));

            var resultado = await servico.BuscarAsync(digitado, false);

            Assert.Equal("Açúcar Mascavo", Assert.Single(resultado).Nome);
        }

        [Fact]
        public async Task Busca_sem_resultado_devolve_lista_vazia_e_nao_erro()
        {
            var (servico, repo, _) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Malte Pilsen"));

            var resultado = await servico.BuscarAsync("xyz", false);

            Assert.NotNull(resultado);
            Assert.Empty(resultado);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Busca_em_branco_devolve_lista_vazia(string? vazio)
        {
            var (servico, repo, _) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Malte Pilsen"));

            Assert.Empty(await servico.BuscarAsync(vazio, false));
        }

        [Fact]
        public async Task Busca_devolve_no_maximo_10_itens()
        {
            var (servico, repo, _) = CriarComLotes();
            for (var i = 1; i <= 15; i++) repo.Dados.Add(NovoInsumo(i, $"Malte {i:00}"));

            var resultado = await servico.BuscarAsync("malte", false);

            Assert.Equal(10, resultado.Count);
            Assert.Equal("Malte 01", resultado.First().Nome);   // os 10 primeiros em ordem alfabética
            Assert.Equal("Malte 10", resultado.Last().Nome);
        }

        [Fact]
        public async Task Busca_com_exatamente_10_ou_menos_devolve_todos()
        {
            var (servico, repo, _) = CriarComLotes();
            for (var i = 1; i <= 10; i++) repo.Dados.Add(NovoInsumo(i, $"Malte {i:00}"));

            Assert.Equal(10, (await servico.BuscarAsync("malte", false)).Count);
        }

        [Fact]
        public async Task Nomes_que_comecam_com_o_termo_vem_antes_dos_que_apenas_contem()
        {
            var (servico, repo, _) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Extrato de Lúpulo"));
            repo.Dados.Add(NovoInsumo(2, "Lúpulo Cascade"));
            repo.Dados.Add(NovoInsumo(3, "Lúpulo Amarillo"));

            var resultado = await servico.BuscarAsync("lupulo", false);

            Assert.Equal(new[] { "Lúpulo Amarillo", "Lúpulo Cascade", "Extrato de Lúpulo" }, resultado.Select(r => r.Nome));
        }

        [Fact]
        public async Task Busca_traz_id_nome_unidade_e_saldo_somando_os_lotes_utilizaveis()
        {
            var (servico, repo, lotes) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(7, "Lúpulo Cascade", UnidadeMedida.G));
            lotes.Dados.Add(NovoLote(7, 300));
            lotes.Dados.Add(NovoLote(7, 450.5m));

            var item = Assert.Single(await servico.BuscarAsync("lupulo", false));

            Assert.Equal(7, item.Id);
            Assert.Equal("Lúpulo Cascade", item.Nome);
            Assert.Equal(UnidadeMedida.G, item.UnidadeMedida);
            Assert.Equal(750.5m, item.SaldoAtual);
        }

        [Fact]
        public async Task Saldo_ignora_lote_vencido_e_lote_esgotado_como_o_armario()
        {
            var (servico, repo, lotes) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Malte Pilsen"));
            lotes.Dados.Add(NovoLote(1, 100, validadeEmDias: 30));    // vale
            lotes.Dados.Add(NovoLote(1, 80, validadeEmDias: -1));     // vencido
            lotes.Dados.Add(NovoLote(1, 0, validadeEmDias: 30));      // esgotado
            lotes.Dados.Add(NovoLote(1, 5, validadeEmDias: 0));       // vence hoje ainda vale

            Assert.Equal(105m, Assert.Single(await servico.BuscarAsync("malte", false)).SaldoAtual);
        }

        [Fact]
        public async Task Insumo_sem_lote_aparece_com_saldo_zero()
        {
            var (servico, repo, _) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Malte Pilsen"));

            Assert.Equal(0m, Assert.Single(await servico.BuscarAsync("malte", false)).SaldoAtual);
        }

        [Fact]
        public async Task Saldo_de_cada_insumo_nao_se_mistura_com_o_do_outro()
        {
            var (servico, repo, lotes) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Lúpulo Cascade"));
            repo.Dados.Add(NovoInsumo(2, "Lúpulo Citra"));
            lotes.Dados.Add(NovoLote(1, 10));
            lotes.Dados.Add(NovoLote(2, 99));

            var resultado = await servico.BuscarAsync("lupulo", false);

            Assert.Equal(10m, resultado.Single(r => r.Id == 1).SaldoAtual);
            Assert.Equal(99m, resultado.Single(r => r.Id == 2).SaldoAtual);
        }

        [Fact]
        public async Task Insumo_inativo_fica_fora_da_busca_e_entra_so_quando_pedido()
        {
            var (servico, repo, _) = CriarComLotes();
            repo.Dados.Add(NovoInsumo(1, "Malte Pilsen"));
            repo.Dados.Add(NovoInsumo(2, "Malte Antigo", ativo: false));

            Assert.Equal("Malte Pilsen", Assert.Single(await servico.BuscarAsync("malte", incluirInativos: false)).Nome);
            Assert.Equal(2, (await servico.BuscarAsync("malte", incluirInativos: true)).Count);
        }

        [Fact]
        public void A_resposta_da_busca_e_leve_so_id_nome_unidade_e_saldo()
        {
            var campos = typeof(InsumoBuscaResponse).GetProperties().Select(p => p.Name).OrderBy(n => n);

            Assert.Equal(new[] { "Id", "Nome", "SaldoAtual", "UnidadeMedida" }, campos);
        }

        private static InsumoRequest Valido(string nome = "Malte Pilsen") => new()
        {
            Nome = nome,
            Categoria = "MALTE",
            UnidadeMedida = "KG",
            EstoqueMinimo = 10
        };

        [Fact]
        public async Task Cria_insumo_valido_com_padroes_de_validade()
        {
            var (servico, _) = Criar();
            var criado = await servico.CriarAsync(Valido());

            Assert.Equal("Malte Pilsen", criado.Nome);
            Assert.Equal(CategoriaInsumo.MALTE, criado.Categoria);
            Assert.Equal(15, criado.DiasValidadeCritica);
            Assert.Equal(45, criado.DiasValidadeAtencao);
            Assert.True(criado.Ativo);
        }

        [Fact]
        public async Task Nome_repetido_gera_conflito_sem_diferenciar_maiusculas()
        {
            var (servico, _) = Criar();
            await servico.CriarAsync(Valido("Malte Pilsen"));

            await Assert.ThrowsAsync<ConflitoException>(() => servico.CriarAsync(Valido("  malte pilsen ")));
        }

        [Fact]
        public async Task Categoria_e_unidade_invalidas_listam_os_campos()
        {
            var (servico, _) = Criar();
            var request = Valido();
            request.Categoria = "XYZ";
            request.UnidadeMedida = "TONELADA";

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(request));
            Assert.Contains("categoria", ex.Detalhes.Keys);
            Assert.Contains("unidadeMedida", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Estoque_minimo_negativo_e_recusado()
        {
            var (servico, _) = Criar();
            var request = Valido();
            request.EstoqueMinimo = -1;

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(request));
            Assert.Contains("estoqueMinimo", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Dias_de_atencao_menores_que_os_criticos_sao_recusados()
        {
            var (servico, _) = Criar();
            var request = Valido();
            request.DiasValidadeCritica = 30;
            request.DiasValidadeAtencao = 10;

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(request));
            Assert.Contains("diasValidadeAtencao", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Atualizar_permite_manter_o_proprio_nome()
        {
            var (servico, _) = Criar();
            var criado = await servico.CriarAsync(Valido());

            var request = Valido();
            request.EstoqueMinimo = 25;
            var atualizado = await servico.AtualizarAsync(criado.Id, request);

            Assert.Equal(25, atualizado.EstoqueMinimo);
        }

        [Fact]
        public async Task Atualizar_para_nome_de_outro_insumo_gera_conflito()
        {
            var (servico, _) = Criar();
            await servico.CriarAsync(Valido("Malte Pilsen"));
            var outro = await servico.CriarAsync(Valido("Malte Munich"));

            await Assert.ThrowsAsync<ConflitoException>(() => servico.AtualizarAsync(outro.Id, Valido("Malte Pilsen")));
        }

        [Fact]
        public async Task Insumo_inexistente_gera_nao_encontrado()
        {
            var (servico, _) = Criar();
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterAsync(999));
        }

        [Fact]
        public async Task Inativar_nao_exclui_e_some_da_listagem_padrao()
        {
            var (servico, repo) = Criar();
            var criado = await servico.CriarAsync(Valido());

            var inativo = await servico.AlterarStatusAsync(criado.Id, false);

            Assert.False(inativo.Ativo);
            Assert.Single(repo.Dados);                                              // continua no banco
            Assert.Empty(await servico.ListarAsync(null, null, false));             // some da lista padrão
            Assert.Single(await servico.ListarAsync(null, null, true));             // aparece com incluirInativos
        }

        [Fact]
        public async Task Lista_filtra_por_categoria_e_busca()
        {
            var (servico, _) = Criar();
            await servico.CriarAsync(Valido("Malte Pilsen"));
            var lupulo = Valido("Lúpulo Cascade");
            lupulo.Categoria = "LUPULO";
            lupulo.UnidadeMedida = "G";
            await servico.CriarAsync(lupulo);

            Assert.Single(await servico.ListarAsync("lupulo", null, false));
            Assert.Single(await servico.ListarAsync(null, "pilsen", false));
            await Assert.ThrowsAsync<ValidacaoException>(() => servico.ListarAsync("INEXISTENTE", null, false));
        }
    }
}
