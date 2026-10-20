using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/receitas")]
    [Produces("application/json")]
    [Authorize]
    public class ReceitasController : ControllerBase
    {
        private readonly IReceitaService _receitas;

        public ReceitasController(IReceitaService receitas) => _receitas = receitas;

        /// <summary>Lista as receitas, com busca por nome ou estilo.</summary>
        /// <param name="busca">Parte do nome ou do estilo.</param>
        /// <param name="incluirInativas">Quando true, traz também as receitas inativas.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<ReceitaResumoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<ReceitaResumoResponse>>> Listar(
            [FromQuery] string? busca, [FromQuery] bool incluirInativas = false, CancellationToken ct = default) =>
            Ok(await _receitas.ListarAsync(busca, incluirInativas, ct));

        /// <summary>Busca a ficha completa de uma receita, com os ingredientes.</summary>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(ReceitaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ReceitaResponse>> ObterPorId(long id, CancellationToken ct) =>
            Ok(await _receitas.ObterAsync(id, ct));

        /// <summary>Cria uma receita com os ingredientes (somente ADMIN).</summary>
        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(ReceitaResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<ReceitaResponse>> Criar([FromBody] ReceitaRequest request, CancellationToken ct)
        {
            var criada = await _receitas.CriarAsync(request, ct);
            return CreatedAtAction(nameof(ObterPorId), new { id = criada.Id }, criada);
        }

        /// <summary>Edita a receita e troca a lista de ingredientes (somente ADMIN).</summary>
        [HttpPut("{id:long}")]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(ReceitaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<ReceitaResponse>> Atualizar(long id, [FromBody] ReceitaRequest request, CancellationToken ct) =>
            Ok(await _receitas.AtualizarAsync(id, request, ct));

        /// <summary>Ativa ou inativa a receita (somente ADMIN). A receita inativa não pode ser produzida.</summary>
        [HttpPatch("{id:long}/status")]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(ReceitaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ReceitaResponse>> AlterarStatus(long id, [FromBody] AlterarStatusRequest request, CancellationToken ct) =>
            Ok(await _receitas.AlterarStatusAsync(id, request.Ativo!.Value, ct));
    }
}
