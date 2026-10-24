using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/alertas")]
    [Produces("application/json")]
    [Authorize]
    public class AlertasController : ControllerBase
    {
        private readonly IAlertasService _alertas;

        public AlertasController(IAlertasService alertas) => _alertas = alertas;


        /// Cada alerta traz <c>acao</c>: VER_LOTES (validade crítica ou em atenção), REGISTRAR_PERDA (lote vencido),
        /// DAR_ENTRADA (sem estoque) ou VER_COMPRAS (abaixo do mínimo), mais <c>insumoId</c>, <c>loteId</c> (nulo em DAR_ENTRADA
        /// e VER_COMPRAS) e <c>unidadeMedida</c>. A <c>severidade</c> monta os grupos da tela: CRITICA e ALTA são "Urgente";
        /// MEDIA e BAIXA são "Atenção". Ordem: urgentes antes e, em cada grupo, menos dias para vencer primeiro.
        /// Lote com saldo zero não gera alerta.
        /// </remarks>
        /// <param name="tipo">VENCIDO, VALIDADE_CRITICA, ATENCAO, ESTOQUE_VAZIO ou ESTOQUE_BAIXO.</param>
        /// <param name="severidade">BAIXA, MEDIA, ALTA ou CRITICA. Combina com o tipo.</param>
        /// <param name="limite">Máximo de alertas devolvidos, depois de ordenar (de 1 a 500). Sem limite, devolve todos.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<AlertaResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<AlertaResponse>>> Listar(
            [FromQuery] TipoAlerta? tipo, [FromQuery] SeveridadeAlerta? severidade, [FromQuery] int? limite, CancellationToken ct = default) =>
            Ok(await _alertas.ListarAsync(tipo, severidade, limite, ct));
    }
}
