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
        private readonly IHistoricoService _historico;

        public BateladasController(IBateladaService bateladas, IHistoricoService historico)
        {
            _bateladas = bateladas;
            _historico = historico;
        }

        /// <summary>Histórico de produções: uma linha por produção, das mais recentes para as mais antigas.</summary>
        /// <remarks>
        /// Cada linha traz código (ex.: B-2026-0012), receita, data, quantidade produzida, custo total, responsável e situação.
        /// Os lotes consumidos não vêm aqui: use GET /bateladas/{id}/consumos.
        /// O período usa datas do calendário (inclusive nos dois lados) no horário do servidor.
        /// </remarks>
        /// <param name="de">Primeiro dia do período (AAAA-MM-DD).</param>
        /// <param name="ate">Último dia do período (AAAA-MM-DD).</param>
        /// <param name="receitaId">Só as produções dessa receita.</param>
        /// <param name="status">EM_ANDAMENTO, FINALIZADA ou CANCELADA.</param>
        /// <param name="pagina">Página, a partir de 1 (padrão 1).</param>
        /// <param name="tamanho">Itens por página, de 1 a 100 (padrão 20).</param>
        [HttpGet]
        [ProducesResponseType(typeof(PaginaResponse<BateladaResumoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PaginaResponse<BateladaResumoResponse>>> Listar(
            [FromQuery] DateOnly? de, [FromQuery] DateOnly? ate, [FromQuery] long? receitaId, [FromQuery] string? status,
            [FromQuery] int? pagina, [FromQuery] int? tamanho, CancellationToken ct) =>
            Ok(await _historico.ListarBateladasAsync(de, ate, receitaId, status, pagina, tamanho, ct));

        /// <summary>Produz uma batelada: baixa o estoque por FEFO em uma única transação.</summary>
        /// <remarks>
        /// Escala a receita pelo volume, trava os lotes, consome primeiro os que vencem antes e registra
        /// a batelada, os lotes consumidos e a movimentação SAIDA_PRODUCAO. Se faltar qualquer ingrediente,
        /// responde 422 ESTOQUE_INSUFICIENTE com a lista de faltas e nada é abatido. Se só houver lote vencido
        /// de um insumo, responde 422 LOTE_VENCIDO (lote vencido nunca é consumido).
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

        /// <summary>Lotes consumidos por uma produção.</summary>
        /// <remarks>Cada linha traz o insumo, o código do lote, a quantidade tirada, a validade do lote e o custo. 404 se a produção não existe.</remarks>
        [HttpGet("{id:long}/consumos")]
        [ProducesResponseType(typeof(List<BateladaConsumoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<List<BateladaConsumoResponse>>> Consumos(long id, CancellationToken ct) =>
            Ok(await _bateladas.ObterConsumosAsync(id, ct));
    }
}
