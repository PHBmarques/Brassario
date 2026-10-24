using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IArmarioService
    {
        Task<List<ArmarioItemResponse>> ListarAsync(string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default);
        Task<ArmarioDetalheResponse> ObterDetalheAsync(long insumoId, bool incluirEsgotados, CancellationToken ct = default);

        /// <summary>API-19: um cartão por insumo (inclusive os sem lote), com saldo, lotes, validade mais próxima e situação.</summary>
        Task<List<InsumoResumoResponse>> ResumirInsumosAsync(string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default);

        /// <summary>API-19: lotes do insumo com saldo na ordem de uso e, em lista separada, os esgotados. 404 se o insumo não existe.</summary>
        Task<InsumoLotesResponse> ObterLotesDoInsumoAsync(long insumoId, CancellationToken ct = default);
    }
}
