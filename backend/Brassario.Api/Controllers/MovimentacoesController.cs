using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/movimentacoes")]
    [Produces("application/json")]
    [Authorize]
    public class MovimentacoesController : ControllerBase
    {
        private readonly IMovimentacaoService _movimentacoes;

        public MovimentacoesController(IMovimentacaoService movimentacoes) => _movimentacoes = movimentacoes;

        /// <summary>Lista o histórico de estoque, com filtros por tipo, lote, insumo, usuário, batelada e período.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<MovimentacaoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<MovimentacaoResponse>>> Listar(
            [FromQuery] MovimentacaoFiltro filtro, CancellationToken ct = default) =>
            Ok(await _movimentacoes.ListarAsync(filtro, ct));
    }
}
