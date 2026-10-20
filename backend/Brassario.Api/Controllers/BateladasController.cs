using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/bateladas")]
    [Produces("application/json")]
    [Authorize]
    public class BateladasController : ControllerBase
    {
        private readonly IBateladaService _bateladas;

        public BateladasController(IBateladaService bateladas) => _bateladas = bateladas;

        /// <summary>Produz uma batelada: baixa o estoque por FEFO em uma única transação.</summary>
        /// <remarks>
        /// Escala a receita pelo volume, trava os lotes, consome primeiro os que vencem antes e registra
        /// a batelada, os lotes consumidos e a movimentação SAIDA_PRODUCAO. Se faltar qualquer ingrediente,
        /// responde 422 ESTOQUE_INSUFICIENTE com a lista de faltas e nada é abatido.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(BateladaResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<BateladaResponse>> Produzir([FromBody] BateladaRequest request, CancellationToken ct)
        {
            var usuarioId = User.ObterUsuarioId();
            if (usuarioId is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var batelada = await _bateladas.ProduzirAsync(request, usuarioId.Value, ct);
            return StatusCode(StatusCodes.Status201Created, batelada);
        }
    }
}
