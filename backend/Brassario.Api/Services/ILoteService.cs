using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface ILoteService
    {
        /// <summary>Registra o recebimento de um insumo: cria o lote e a movimentação ENTRADA na mesma transação.</summary>
        Task<LoteResponse> RegistrarEntradaAsync(LoteRequest request, long usuarioId, CancellationToken ct = default);
    }
}
