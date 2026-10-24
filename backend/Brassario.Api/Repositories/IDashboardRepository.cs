using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public record ConsumoPorInsumo(long InsumoId, decimal Quantidade);

    public interface IDashboardRepository
    {
        Task<List<Insumo>> ListarInsumosAtivosAsync(CancellationToken ct = default);
        Task<List<LoteInsumo>> ListarLotesAsync(IReadOnlyCollection<long> insumoIds, CancellationToken ct = default);
        Task<List<ConsumoPorInsumo>> ListarConsumoDesdeAsync(
            IReadOnlyCollection<long> insumoIds, DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default);
    }
}
