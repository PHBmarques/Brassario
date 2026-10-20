using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/fornecedores")]
    [Produces("application/json")]
    [Authorize]
    public class FornecedoresController : ControllerBase
    {
        private readonly IFornecedorService _fornecedores;

        public FornecedoresController(IFornecedorService fornecedores) => _fornecedores = fornecedores;

        /// <summary>Lista os fornecedores em ordem de nome, com busca opcional.</summary>
        /// <param name="busca">Parte do nome do fornecedor.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<FornecedorResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<FornecedorResponse>>> Listar([FromQuery] string? busca, CancellationToken ct) =>
            Ok(await _fornecedores.ListarAsync(busca, ct));

        /// <summary>Busca um fornecedor pelo id.</summary>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(FornecedorResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<FornecedorResponse>> ObterPorId(long id, CancellationToken ct) =>
            Ok(await _fornecedores.ObterAsync(id, ct));

        /// <summary>Cadastra um fornecedor (somente ADMIN). O nome não pode repetir.</summary>
        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(FornecedorResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<FornecedorResponse>> Criar([FromBody] FornecedorRequest request, CancellationToken ct)
        {
            var criado = await _fornecedores.CriarAsync(request, ct);
            return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
        }

        /// <summary>Edita um fornecedor (somente ADMIN). O nome não pode repetir.</summary>
        [HttpPut("{id:long}")]
        [Authorize(Roles = "ADMIN")]
        [ProducesResponseType(typeof(FornecedorResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<FornecedorResponse>> Atualizar(long id, [FromBody] FornecedorRequest request, CancellationToken ct) =>
            Ok(await _fornecedores.AtualizarAsync(id, request, ct));
    }
}
