using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface IFornecedorCadastroRepository : IFornecedorRepository
    {
        Task<List<Fornecedor>> ListarAsync(string? busca, CancellationToken ct = default);

        /// <summary>Já existe fornecedor com esse nome? (sem diferenciar maiúsculas; ignora o id informado na edição)</summary>
        Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default);

        Task AdicionarAsync(Fornecedor fornecedor, CancellationToken ct = default);

        Task SalvarAsync(CancellationToken ct = default);
    }
}
