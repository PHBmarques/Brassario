using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IDisponibilidadeService
    {
        /// <summary>
        /// Simula a produção da receita para o volume (em litros), sem alterar o banco nem bloquear lotes.
        /// Erros: 400 (volume inválido), 404 (receita não existe), 422 STATUS_INVALIDO (receita inativa).
        /// </summary>
        Task<DisponibilidadeResponse> SimularAsync(long receitaId, decimal? volume, CancellationToken ct = default);
    }
}
