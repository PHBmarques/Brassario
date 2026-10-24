using Brassario.Api.DTOs.Response;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/armario")]
    [Produces("application/json")]
    [Authorize]
    public class ArmarioController : ControllerBase
    {
        private readonly IArmarioService _armario;

        public ArmarioController(IArmarioService armario) => _armario = armario;

        /// <summary>Resumo do estoque por insumo: saldo (só lotes não vencidos), escassez, próximo vencimento e cor.</summary>
        /// <param name="categoria">MALTE, LUPULO, LEVEDURA, ADJUNTO ou OUTRO.</param>
        /// <param name="busca">Parte do nome do insumo.</param>
        /// <param name="incluirInativos">Quando true, traz também os insumos inativos.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<ArmarioItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<ArmarioItemResponse>>> Listar(
            [FromQuery] string? categoria, [FromQuery] string? busca, [FromQuery] bool incluirInativos = false,
            CancellationToken ct = default) =>
            Ok(await _armario.ListarAsync(categoria, busca, incluirInativos, ct));


        [HttpGet("{insumoId:long}")]
        [ProducesResponseType(typeof(ArmarioDetalheResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ArmarioDetalheResponse>> ObterDetalhe(
            long insumoId, [FromQuery] bool incluirEsgotados = false, CancellationToken ct = default) =>
            Ok(await _armario.ObterDetalheAsync(insumoId, incluirEsgotados, ct));
    }
}
