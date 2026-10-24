using Brassario.Api.DTOs.Request;
using Brassario.Api.Models;
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
        private readonly IMovimentacaoService _movimentacoes;

        public LotesController(ILoteService lotes, IMovimentacaoService movimentacoes)
        {
            _lotes = lotes;
            _movimentacoes = movimentacoes;
        }

        /// <summary>Registra uma perda no lote e abate o saldo, mantendo histórico do motivo e do usuário.</summary>
        [HttpPost("{loteId:long}/perdas")]
        [Authorize(Roles = "ADMIN,OPERADOR")]
        [ProducesResponseType(typeof(MovimentacaoResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<MovimentacaoResponse>> RegistrarPerda(
            long loteId, [FromBody] PerdaRequest request, CancellationToken ct)
        {
            var usuarioId = User.ObterUsuarioId();
            if (usuarioId is null) return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));
            var movimentacao = await _movimentacoes.RegistrarPerdaAsync(loteId, request, usuarioId.Value, ct);
            return StatusCode(StatusCodes.Status201Created, movimentacao);
        }

        /// <summary>Registra ajuste positivo ou negativo no saldo do lote. Apenas ADMIN.</summary>
        [HttpPost("{loteId:long}/ajustes")]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(MovimentacaoResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<MovimentacaoResponse>> RegistrarAjuste(
            long loteId, [FromBody] AjusteRequest request, CancellationToken ct)
        {
            var usuarioId = User.ObterUsuarioId();
            if (usuarioId is null) return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));
            var movimentacao = await _movimentacoes.RegistrarAjusteAsync(loteId, request, usuarioId.Value, ct);
            return StatusCode(StatusCodes.Status201Created, movimentacao);
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
    }
}

