using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IFornecedorService
    {
        Task<List<FornecedorResponse>> ListarAsync(string? busca, CancellationToken ct = default);
        Task<FornecedorResponse> ObterAsync(long id, CancellationToken ct = default);
        Task<FornecedorResponse> CriarAsync(FornecedorRequest request, CancellationToken ct = default);
        Task<FornecedorResponse> AtualizarAsync(long id, FornecedorRequest request, CancellationToken ct = default);
    }
}
