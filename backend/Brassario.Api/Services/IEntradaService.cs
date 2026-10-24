using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IEntradaService
    {
        /// <summary>
        /// Registra a entrada de estoque, cadastrando antes (na mesma transação) o insumo e/ou o fornecedor, se forem novos.
        /// Erros: 400 (campos, com "detalhes"), 404 (insumo ou fornecedor), 409 NOME_DUPLICADO / CODIGO_LOTE_DUPLICADO,
        /// 422 (insumo ou fornecedor inativo, validade vencida). Se qualquer parte falhar, nada é gravado.
        /// </summary>
        Task<EntradaResponse> RegistrarAsync(EntradaRequest request, long usuarioId, CancellationToken ct = default);
    }
}
