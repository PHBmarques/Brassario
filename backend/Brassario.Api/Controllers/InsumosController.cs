using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/insumos")]
    [Produces("application/json")]
    [Authorize]
    public class InsumosController : ControllerBase
    {
        private readonly IInsumoService _insumos;

        public InsumosController(IInsumoService insumos) => _insumos = insumos;

        /// <summary>Lista os insumos, com filtro por categoria e busca por nome.</summary>
        /// <param name="categoria">MALTE, LUPULO, LEVEDURA, ADJUNTO ou OUTRO.</param>
        /// <param name="busca">Parte do nome do insumo.</param>
        /// <param name="incluirInativos">Quando true, traz também os inativos.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<InsumoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<InsumoResponse>>> Listar(
            [FromQuery] string? categoria, [FromQuery] string? busca, [FromQuery] bool incluirInativos = false,
            CancellationToken ct = default) =>
            Ok(await _insumos.ListarAsync(categoria, busca, incluirInativos, ct));

        /// <summary>Busca um insumo pelo id.</summary>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(InsumoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InsumoResponse>> ObterPorId(long id, CancellationToken ct) =>
            Ok(await _insumos.ObterAsync(id, ct));

        /// <summary>Cadastra um insumo (somente ADMIN).</summary>
        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(InsumoResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<InsumoResponse>> Criar([FromBody] InsumoRequest request, CancellationToken ct)
        {
            var criado = await _insumos.CriarAsync(request, ct);
            return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
        }

        /// <summary>Edita um insumo (somente ADMIN).</summary>
        [HttpPut("{id:long}")]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(InsumoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<InsumoResponse>> Atualizar(long id, [FromBody] InsumoRequest request, CancellationToken ct) =>
            Ok(await _insumos.AtualizarAsync(id, request, ct));

        /// <summary>Ativa ou inativa um insumo (somente ADMIN). Não existe exclusão (RN-16).</summary>
        [HttpPatch("{id:long}/status")]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(InsumoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InsumoResponse>> AlterarStatus(long id, [FromBody] AlterarStatusRequest request, CancellationToken ct) =>
            Ok(await _insumos.AlterarStatusAsync(id, request.Ativo!.Value, ct));
    }
}
