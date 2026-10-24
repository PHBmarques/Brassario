using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IDashboardService
    {
        Task<DashboardResumoResponse> ObterResumoAsync(CancellationToken ct = default);
        Task<List<DashboardConsumoResponse>> ListarConsumoAsync(CancellationToken ct = default);
        Task<List<DashboardPrevisaoResponse>> ListarPrevisaoAsync(CancellationToken ct = default);
        Task<List<SugestaoCompraResponse>> ListarSugestoesCompraAsync(CancellationToken ct = default);
    }
}
