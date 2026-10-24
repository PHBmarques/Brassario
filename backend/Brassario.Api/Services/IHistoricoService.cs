using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IHistoricoService
    {
        /// <summary>
        /// Produções, uma por linha, das mais recentes para as mais antigas, com filtros por período (datas do calendário),
        /// receita e status. Sem os lotes consumidos (veja ObterConsumosAsync).
        /// Erros: 400 (período invertido, status, ids ou paginação inválidos).
        /// </summary>
        Task<PaginaResponse<BateladaResumoResponse>> ListarBateladasAsync(
            DateOnly? de, DateOnly? ate, long? receitaId, string? status, int? pagina, int? tamanho, CancellationToken ct = default);

        /// <summary>
        /// Entradas e saídas de estoque, das mais recentes para as mais antigas, filtradas por insumo, lote e/ou produção.
        /// Erros: 400 (ids ou paginação inválidos).
        /// </summary>
        Task<PaginaResponse<MovimentacaoResponse>> ListarMovimentacoesAsync(
            long? insumoId, long? loteId, long? producaoId, int? pagina, int? tamanho, CancellationToken ct = default);
    }
}
