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

        /// <summary>Lista os fornecedores em ordem de nome ou, com busca, alimenta a caixa de escolha.</summary>
        /// <remarks>
        /// **Sem busca:** lista completa (FornecedorResponse).
        ///
        /// **Com busca:** resposta leve para a caixa de escolha, só fornecedores ativos, no máximo 10 itens
        /// (id, nome), sem diferenciar maiúsculas nem acentos. Os nomes que começam com o termo vêm primeiro.
        /// Busca sem resultado devolve 200 com lista vazia. Permitido para ADMIN e OPERADOR.
        /// </remarks>
        /// <param name="busca">Parte do nome do fornecedor.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<FornecedorResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Listar([FromQuery] string? busca, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(busca))
                return Ok(await _fornecedores.BuscarAsync(busca, ct));

            return Ok(await _fornecedores.ListarAsync(null, ct));
        }

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
