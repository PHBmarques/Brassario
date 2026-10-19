using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/lotes")]
    [Produces("application/json")]
    [Authorize]
    public class LotesController:ControllerBase
    {
        private readonly ILoteService _lotes;

        public LotesController(ILoteService lotes) => _lotes = lotes;

        /// <summary>Registra o recebimento de um insumo como novo lote, com validade, preço e fornecedor.</summary>
        /// <remarks>
        /// Cria o lote (saldo atual igual ao inicial) e a movimentação ENTRADA do usuário logado, na mesma transação.
        /// A resposta traz a situação da validade: VERDE, AMARELO ou VERMELHO.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(LoteResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<LoteResponse>> RegistrarEntrada([FromBody] LoteRequest request, CancellationToken ct)
        {
            var usuarioId = User.ObterUsuarioId();
            if (usuarioId is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var lote = await _lotes.RegistrarEntradaAsync(request, usuarioId.Value, ct);
            return StatusCode(StatusCodes.Status201Created, lote);
        }
    }
}
