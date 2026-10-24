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
        private readonly IArmarioService _armario;

        public InsumosController(IInsumoService insumos, IArmarioService armario)
        {
            _insumos = insumos;
            _armario = armario;
        }

        /// <summary>Lista os insumos (com filtro por categoria) ou, com busca, alimenta a caixa de escolha.</summary>
        /// <remarks>
        /// **Sem busca:** lista completa (InsumoResponse), com filtro opcional por categoria.
        ///
        /// **Com busca:** resposta leve para a caixa de escolha, no máximo 10 itens
        /// (id, nome, unidadeMedida, saldoAtual), sem diferenciar maiúsculas nem acentos. Os nomes que começam
        /// com o termo vêm primeiro. O saldoAtual soma só lotes dentro da validade e com saldo.
        /// Busca sem resultado devolve 200 com lista vazia. Permitido para ADMIN e OPERADOR.
        /// </remarks>
        /// <param name="categoria">MALTE, LUPULO, LEVEDURA, ADJUNTO ou OUTRO (ignorada quando há busca).</param>
        /// <param name="busca">Parte do nome do insumo.</param>
        /// <param name="incluirInativos">Quando true, traz também os inativos.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<InsumoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Listar(
            [FromQuery] string? categoria, [FromQuery] string? busca, [FromQuery] bool incluirInativos = false,
            CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(busca))
                return Ok(await _insumos.BuscarAsync(busca, incluirInativos, ct));

            return Ok(await _insumos.ListarAsync(categoria, null, incluirInativos, ct));
        }

        /// <summary>Resumo do armário: um cartão por insumo, mesmo os que ainda não têm lote (saldo zero).</summary>
        /// <remarks>
        /// Cada item traz saldo total, saldo utilizável, número de lotes com saldo, validade mais próxima e a situação:
        /// OK, ATENCAO, CRITICO, VENCIDO ou ABAIXO_DO_MINIMO (a primeira que valer, nesta ordem: VENCIDO, CRITICO,
        /// ABAIXO_DO_MINIMO, ATENCAO, OK), calculada com os dias de atenção e de validade crítica de cada insumo.
        /// </remarks>
        /// <param name="categoria">MALTE, LUPULO, LEVEDURA, ADJUNTO ou OUTRO.</param>
        /// <param name="busca">Parte do nome do insumo.</param>
        /// <param name="incluirInativos">Quando true, traz também os inativos.</param>
        [HttpGet("resumo")]
        [ProducesResponseType(typeof(List<InsumoResumoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<InsumoResumoResponse>>> ResumoEstoque(
            [FromQuery] string? categoria, [FromQuery] string? busca, [FromQuery] bool incluirInativos = false,
            CancellationToken ct = default) =>
            Ok(await _armario.ResumirInsumosAsync(categoria, busca, incluirInativos, ct));

        /// <summary>Lotes do insumo: os com saldo na ordem de uso e, em lista separada, os esgotados.</summary>
        /// <remarks>
        /// Ordem de uso: validade mais próxima primeiro; empate, quem entrou primeiro. É a mesma regra da baixa de estoque.
        /// Em `lotes` vêm primeiro os que podem ser usados (com `ordemDeUso` 1, 2, 3...) e depois os vencidos que ainda
        /// têm saldo (sem `ordemDeUso`). Os que já zeraram ficam em `esgotados`.
        /// </remarks>
        /// <param name="id">Id do insumo.</param>
        [HttpGet("{id:long}/lotes")]
        [ProducesResponseType(typeof(InsumoLotesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<InsumoLotesResponse>> LotesDoInsumo(long id, CancellationToken ct) =>
            Ok(await _armario.ObterLotesDoInsumoAsync(id, ct));

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
