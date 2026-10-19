using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IArmarioService
    {
        Task<List<ArmarioItemResponse>> ListarAsync(string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default);
        Task<ArmarioDetalheResponse> ObterDetalheAsync(long insumoId, bool incluirEsgotados, CancellationToken ct = default);
    }
}
