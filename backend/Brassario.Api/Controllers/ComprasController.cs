using Brassario.Api.DTOs.Response;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/compras")]
    [Produces("application/json")]
    [Authorize]
    public class ComprasController : ControllerBase
    {
        private readonly IDashboardService _dashboard;

        public ComprasController(IDashboardService dashboard) => _dashboard = dashboard;

        [HttpGet("sugestao")]
        [ProducesResponseType(typeof(List<SugestaoCompraResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<List<SugestaoCompraResponse>>> Sugestao(CancellationToken ct) =>
            Ok(await _dashboard.ListarSugestoesCompraAsync(ct));
    }
}
