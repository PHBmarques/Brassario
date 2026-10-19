using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IInsumoService
    {
        Task<List<InsumoResponse>> ListarAsync(string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default);
        Task<InsumoResponse> ObterAsync(long id, CancellationToken ct = default);
        Task<InsumoResponse> CriarAsync(InsumoRequest request, CancellationToken ct = default);
        Task<InsumoResponse> AtualizarAsync(long id, InsumoRequest request, CancellationToken ct = default);
        Task<InsumoResponse> AlterarStatusAsync(long id, bool ativo, CancellationToken ct = default);
    }
}
