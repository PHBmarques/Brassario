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
    [Tags("Receitas")]
    public class DisponibilidadeController : ControllerBase
    {
        private readonly IDisponibilidadeService _disponibilidade;

        public DisponibilidadeController(IDisponibilidadeService disponibilidade) => _disponibilidade = disponibilidade;

        /// <summary>
        /// Simula a produção para um volume: mostra, por ingrediente, o necessário, o disponível,
        /// o que falta e os lotes que seriam usados (FEFO). Não altera o estoque.
        /// </summary>
        /// <param name="id">Id da receita.</param>
        /// <param name="volume">Volume a produzir, em litros (maior que zero). Use ponto nos decimais, ex.: 50.5.</param>
        [HttpGet("{id:long}/disponibilidade")]
        [ProducesResponseType(typeof(DisponibilidadeResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<DisponibilidadeResponse>> Obter(
            long id, [FromQuery] decimal? volume, CancellationToken ct) =>
            Ok(await _disponibilidade.SimularAsync(id, volume, ct));
    }
}
