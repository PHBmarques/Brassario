using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface IFornecedorRepository
    {
        Task<Fornecedor?> ObterPorIdAsync(long id, CancellationToken ct = default);
    }
}
