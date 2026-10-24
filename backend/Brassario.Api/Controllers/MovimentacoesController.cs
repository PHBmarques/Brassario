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
        private readonly IHistoricoService _historico;

        public MovimentacoesController(IHistoricoService historico) => _historico = historico;

        /// <summary>Entradas e saídas de estoque, das mais recentes para as mais antigas.</summary>
        /// <remarks>
        /// Quantidade positiva é entrada; negativa é saída. Usada no detalhe do insumo. Os filtros se combinam.
        /// </remarks>
        /// <param name="insumoId">Só as movimentações dos lotes desse insumo.</param>
        /// <param name="loteId">Só as movimentações desse lote.</param>
        /// <param name="producaoId">Só as saídas dessa produção.</param>
        /// <param name="pagina">Página, a partir de 1 (padrão 1).</param>
        /// <param name="tamanho">Itens por página, de 1 a 100 (padrão 20).</param>
        [HttpGet]
        [ProducesResponseType(typeof(PaginaResponse<MovimentacaoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PaginaResponse<MovimentacaoResponse>>> Listar(
            [FromQuery] long? insumoId, [FromQuery] long? loteId, [FromQuery] long? producaoId,
            [FromQuery] int? pagina, [FromQuery] int? tamanho, CancellationToken ct) =>
            Ok(await _historico.ListarMovimentacoesAsync(insumoId, loteId, producaoId, pagina, tamanho, ct));
    }
}
