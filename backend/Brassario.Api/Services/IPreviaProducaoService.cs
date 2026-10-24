using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IPreviaProducaoService
    {
        /// <summary>
        /// Simula a baixa FEFO da produção para a quantidade (litros) sem gravar nada e sem bloquear lotes.
        /// Usa o mesmo plano da baixa (PlanoDeBaixa). O resultado pode mudar até a confirmação, que confere tudo de novo.
        /// Erros: 400 (quantidade inválida), 404 (receita não existe), 422 STATUS_INVALIDO (receita inativa).
        /// </summary>
        Task<PreviaProducaoResponse> PreverAsync(long receitaId, decimal? quantidade, CancellationToken ct = default);
    }
}
