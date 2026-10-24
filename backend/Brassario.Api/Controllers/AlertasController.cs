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

        /// <summary>Lista alertas de validade e estoque, ordenados por severidade e proximidade do vencimento.</summary>
        /// <param name="tipo">VENCIDO, VALIDADE_CRITICA, ATENCAO, ESTOQUE_VAZIO ou ESTOQUE_BAIXO.</param>
        /// <param name="severidade">BAIXA, MEDIA, ALTA ou CRITICA.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<AlertaResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<AlertaResponse>>> Listar(
            [FromQuery] TipoAlerta? tipo, [FromQuery] SeveridadeAlerta? severidade, CancellationToken ct = default) =>
            Ok(await _alertas.ListarAsync(tipo, severidade, ct));
    }
}
