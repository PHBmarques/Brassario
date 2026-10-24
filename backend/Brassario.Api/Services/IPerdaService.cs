using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IPerdaService
    {
        /// <summary>
        /// Registra a perda de uma quantidade de um lote (RN-15): baixa o saldo e grava a movimentação PERDA
        /// (negativa, com o motivo) do usuário logado, na mesma transação. Depois de uma perda que zera o lote, o alerta some.
        /// Erros: 400 (quantidade ou motivo inválidos), 404 (lote não existe),
        /// 422 PERDA_MAIOR_QUE_SALDO (nada é alterado).
        /// </summary>
        Task<PerdaResponse> RegistrarAsync(long loteId, PerdaRequest request, long usuarioId, CancellationToken ct = default);
    }
}
