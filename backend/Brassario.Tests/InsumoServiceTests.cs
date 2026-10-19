using Brassario.Api.DTOs.Request;
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

        private static (InsumoService Servico, RepositorioFalso Repo) Criar()
        {
            var repo = new RepositorioFalso();
            return (new InsumoService(repo), repo);
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
