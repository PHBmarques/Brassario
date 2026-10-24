using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    public class FornecedorServiceTests
    {
        private class RepoFalso : IFornecedorCadastroRepository
        {
            public List<Fornecedor> Dados { get; } = new();
            public int Salvamentos { get; private set; }
            private long _proximoId = 1;

            public Task<Fornecedor?> ObterPorIdAsync(long id, CancellationToken ct = default) =>
                Task.FromResult(Dados.FirstOrDefault(f => f.Id == id));

            public Task<List<Fornecedor>> ListarAsync(string? busca, CancellationToken ct = default)
            {
                var q = Dados.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(busca))
                    q = q.Where(f => f.Nome.Contains(busca.Trim(), StringComparison.OrdinalIgnoreCase));
                return Task.FromResult(q.OrderBy(f => f.Nome).ToList());
            }

            public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default) =>
                Task.FromResult(Dados.Any(f =>
                    string.Equals(f.Nome, nome.Trim(), StringComparison.OrdinalIgnoreCase) &&
                    (ignorarId == null || f.Id != ignorarId)));

            public Task AdicionarAsync(Fornecedor fornecedor, CancellationToken ct = default)
            {
                fornecedor.Id = _proximoId++;
                Dados.Add(fornecedor);
                return Task.CompletedTask;
            }

            public Task SalvarAsync(CancellationToken ct = default)
            {
                Salvamentos++;
                return Task.CompletedTask;
            }
        }

        private static (FornecedorService Servico, RepoFalso Repo) Criar()
        {
            var repo = new RepoFalso();
            return (new FornecedorService(repo), repo);
        }

        // ---------- API-17: busca para a caixa de escolha ----------

        [Theory]
        [InlineData("São")]
        [InlineData("sao")]
        [InlineData("SÃO PAULO")]
        [InlineData("  sao paulo ")]
        public async Task Busca_ignora_acento_maiuscula_e_espacos_nas_pontas(string digitado)
        {
            var (servico, repo) = Criar();
            repo.Dados.Add(new Fornecedor { Id = 1, Nome = "Maltaria São Paulo", Ativo = true });
            repo.Dados.Add(new Fornecedor { Id = 2, Nome = "Hop Brasil", Ativo = true });

            var resultado = await servico.BuscarAsync(digitado);

            Assert.Equal("Maltaria São Paulo", Assert.Single(resultado).Nome);
        }

        [Fact]
        public async Task Busca_sem_acento_acha_nome_com_acento()
        {
            var (servico, repo) = Criar();
            repo.Dados.Add(new Fornecedor { Id = 1, Nome = "Cervejaria Açoriana", Ativo = true });

            Assert.Equal("Cervejaria Açoriana", Assert.Single(await servico.BuscarAsync("acoriana")).Nome);
        }

        [Fact]
        public async Task Busca_sem_resultado_devolve_lista_vazia_e_nao_erro()
        {
            var (servico, repo) = Criar();
            repo.Dados.Add(new Fornecedor { Id = 1, Nome = "Hop Brasil", Ativo = true });

            var resultado = await servico.BuscarAsync("xyz");

            Assert.NotNull(resultado);
            Assert.Empty(resultado);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Busca_em_branco_devolve_lista_vazia(string? vazio)
        {
            var (servico, repo) = Criar();
            repo.Dados.Add(new Fornecedor { Id = 1, Nome = "Hop Brasil", Ativo = true });

            Assert.Empty(await servico.BuscarAsync(vazio));
        }

        [Fact]
        public async Task Busca_devolve_no_maximo_10_itens()
        {
            var (servico, repo) = Criar();
            for (var i = 1; i <= 15; i++) repo.Dados.Add(new Fornecedor { Id = i, Nome = $"Distribuidora {i:00}", Ativo = true });

            var resultado = await servico.BuscarAsync("distribuidora");

            Assert.Equal(10, resultado.Count);
            Assert.Equal("Distribuidora 01", resultado.First().Nome);
            Assert.Equal("Distribuidora 10", resultado.Last().Nome);
        }

        [Fact]
        public async Task Busca_so_devolve_fornecedores_ativos()
        {
            var (servico, repo) = Criar();
            repo.Dados.Add(new Fornecedor { Id = 1, Nome = "Hop Brasil", Ativo = true });
            repo.Dados.Add(new Fornecedor { Id = 2, Nome = "Hop Antigo", Ativo = false });

            var resultado = await servico.BuscarAsync("hop");

            Assert.Equal("Hop Brasil", Assert.Single(resultado).Nome);
        }

        [Fact]
        public async Task Inativos_ficam_fora_do_limite_de_10()
        {
            var (servico, repo) = Criar();
            for (var i = 1; i <= 12; i++) repo.Dados.Add(new Fornecedor { Id = i, Nome = $"Hop {i:00}", Ativo = false });
            repo.Dados.Add(new Fornecedor { Id = 99, Nome = "Hop Ativo", Ativo = true });

            Assert.Equal("Hop Ativo", Assert.Single(await servico.BuscarAsync("hop")).Nome);
        }

        [Fact]
        public void A_resposta_da_busca_e_leve_so_id_e_nome()
        {
            var campos = typeof(Brassario.Api.DTOs.Response.FornecedorBuscaResponse).GetProperties().Select(p => p.Name).OrderBy(n => n);

            Assert.Equal(new[] { "Id", "Nome" }, campos);
        }

        [Fact]
        public async Task Cria_fornecedor_ativo_com_nome_e_contato_sem_espacos_sobrando()
        {
            var (servico, repo) = Criar();

            var r = await servico.CriarAsync(new FornecedorRequest { Nome = "  Hop Brasil  ", Contato = "  contato@hopbrasil.example " });

            Assert.Equal("Hop Brasil", r.Nome);
            Assert.Equal("contato@hopbrasil.example", r.Contato);
            Assert.True(r.Ativo);
            Assert.True(r.Id > 0);
            Assert.Single(repo.Dados);
            Assert.Equal(1, repo.Salvamentos);
        }

        [Fact]
        public async Task Contato_vazio_vira_nulo()
        {
            var (servico, _) = Criar();

            var r = await servico.CriarAsync(new FornecedorRequest { Nome = "Maltaria Sul", Contato = "   " });

            Assert.Null(r.Contato);
        }

        [Theory]
        [InlineData("Hop Brasil")]
        [InlineData("hop brasil")]
        [InlineData("  HOP BRASIL ")]
        public async Task Nome_repetido_gera_409_sem_diferenciar_maiusculas(string nome)
        {
            var (servico, repo) = Criar();
            await servico.CriarAsync(new FornecedorRequest { Nome = "Hop Brasil" });

            await Assert.ThrowsAsync<ConflitoException>(() => servico.CriarAsync(new FornecedorRequest { Nome = nome }));
            Assert.Single(repo.Dados);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Nome_vazio_gera_400(string nome)
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(new FornecedorRequest { Nome = nome }));
            Assert.True(ex.Detalhes.ContainsKey("nome"));
        }

        [Fact]
        public async Task Nome_e_contato_longos_demais_geram_400_com_os_dois_campos()
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.CriarAsync(new FornecedorRequest { Nome = new string('a', 121), Contato = new string('b', 161) }));

            Assert.True(ex.Detalhes.ContainsKey("nome"));
            Assert.True(ex.Detalhes.ContainsKey("contato"));
        }

        [Fact]
        public async Task Atualiza_nome_e_contato()
        {
            var (servico, repo) = Criar();
            var criado = await servico.CriarAsync(new FornecedorRequest { Nome = "Hop Brasil", Contato = "antigo" });

            var r = await servico.AtualizarAsync(criado.Id, new FornecedorRequest { Nome = "Hop Brasil Ltda", Contato = "novo" });

            Assert.Equal("Hop Brasil Ltda", r.Nome);
            Assert.Equal("novo", r.Contato);
            Assert.Equal("Hop Brasil Ltda", repo.Dados.Single().Nome);
        }

        [Fact]
        public async Task Atualizar_mantendo_o_proprio_nome_nao_da_conflito()
        {
            var (servico, _) = Criar();
            var criado = await servico.CriarAsync(new FornecedorRequest { Nome = "Hop Brasil" });

            var r = await servico.AtualizarAsync(criado.Id, new FornecedorRequest { Nome = "hop brasil", Contato = "novo" });

            Assert.Equal("hop brasil", r.Nome);
        }

        [Fact]
        public async Task Atualizar_para_o_nome_de_outro_fornecedor_gera_409()
        {
            var (servico, repo) = Criar();
            await servico.CriarAsync(new FornecedorRequest { Nome = "Hop Brasil" });
            var segundo = await servico.CriarAsync(new FornecedorRequest { Nome = "Maltaria Sul" });

            await Assert.ThrowsAsync<ConflitoException>(() =>
                servico.AtualizarAsync(segundo.Id, new FornecedorRequest { Nome = "Hop Brasil" }));
            Assert.Equal("Maltaria Sul", repo.Dados.Single(f => f.Id == segundo.Id).Nome);
        }

        [Fact]
        public async Task Atualizar_inexistente_gera_404()
        {
            var (servico, _) = Criar();

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
                servico.AtualizarAsync(99, new FornecedorRequest { Nome = "X" }));
        }

        [Fact]
        public async Task Obter_inexistente_gera_404()
        {
            var (servico, _) = Criar();

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterAsync(99));
        }

        [Fact]
        public async Task Lista_em_ordem_de_nome_e_filtra_pela_busca()
        {
            var (servico, _) = Criar();
            await servico.CriarAsync(new FornecedorRequest { Nome = "Maltaria Sul" });
            await servico.CriarAsync(new FornecedorRequest { Nome = "Hop Brasil" });
            await servico.CriarAsync(new FornecedorRequest { Nome = "Levedura Pura" });

            var todos = await servico.ListarAsync(null);
            var filtrado = await servico.ListarAsync("  MALT ");

            Assert.Equal(new[] { "Hop Brasil", "Levedura Pura", "Maltaria Sul" }, todos.Select(f => f.Nome).ToArray());
            Assert.Equal("Maltaria Sul", Assert.Single(filtrado).Nome);
        }
    }
}
