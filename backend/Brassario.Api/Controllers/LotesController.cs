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
        private readonly IPerdaService _perdas;

        public LotesController(ILoteService lotes, IPerdaService perdas)
        {
            _lotes = lotes;
            _perdas = perdas;
        }

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

        /// <summary>Registra a perda de uma quantidade de um lote (RN-15). Liberado para ADMIN e OPERADOR.</summary>
        /// <remarks>
        /// Baixa o saldo do lote e grava a movimentação PERDA (negativa, com o motivo e o usuário logado), na mesma transação.
        /// O motivo é um texto de 5 a 255 caracteres; a lista de motivos da tela é só do front.
        /// Se a perda zera o lote, o alerta dele some do GET /alertas.
        /// </remarks>
        /// <param name="id">Id do lote.</param>
        [HttpPost("{id:long}/perdas")]
        [ProducesResponseType(typeof(PerdaResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<PerdaResponse>> RegistrarPerda(long id, [FromBody] PerdaRequest request, CancellationToken ct)
        {
            var usuarioId = User.ObterUsuarioId();
            if (usuarioId is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var perda = await _perdas.RegistrarAsync(id, request, usuarioId.Value, ct);
            return StatusCode(StatusCodes.Status201Created, perda);
        }
    }
}
