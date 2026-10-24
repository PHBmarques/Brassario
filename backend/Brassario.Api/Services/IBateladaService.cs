using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IBateladaService
    {
        /// <summary>
        /// Confirma a produção: baixa o estoque por FEFO, registra o consumo por lote e a movimentação
        /// SAIDA_PRODUCAO, tudo em uma única transação (tudo ou nada).
        /// Erros: 400 (dados inválidos), 404 (receita não existe), 422 STATUS_INVALIDO (receita inativa),
        /// 422 ESTOQUE_INSUFICIENTE (com a lista de faltas; nada é abatido).
        /// </summary>
        Task<BateladaResponse> ProduzirAsync(BateladaRequest request, long usuarioId, CancellationToken ct = default);

        Task<BateladaResponse> FinalizarAsync(long id, FinalizarBateladaRequest request, CancellationToken ct = default);

        Task<BateladaResponse> CancelarAsync(long id, CancelarBateladaRequest request, long usuarioId, CancellationToken ct = default);
    }
}
