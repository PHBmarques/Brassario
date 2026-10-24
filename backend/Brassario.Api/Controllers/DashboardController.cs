using Brassario.Api.DTOs.Response;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/dashboard")]
    [Produces("application/json")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboard;

        public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

        [HttpGet("resumo")]
        [ProducesResponseType(typeof(DashboardResumoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<DashboardResumoResponse>> Resumo(CancellationToken ct) =>
            Ok(await _dashboard.ObterResumoAsync(ct));

        [HttpGet("consumo")]
        [ProducesResponseType(typeof(List<DashboardConsumoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<DashboardConsumoResponse>>> Consumo(CancellationToken ct) =>
            Ok(await _dashboard.ListarConsumoAsync(ct));

        [HttpGet("previsao")]
        [ProducesResponseType(typeof(List<DashboardPrevisaoResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<DashboardPrevisaoResponse>>> Previsao(CancellationToken ct) =>
            Ok(await _dashboard.ListarPrevisaoAsync(ct));
    }
}
