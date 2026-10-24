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
        private readonly IPreviaProducaoService _previa;

        public DisponibilidadeController(IDisponibilidadeService disponibilidade, IPreviaProducaoService previa)
        {
            _disponibilidade = disponibilidade;
            _previa = previa;
        }

        /// <summary>
        /// Prévia da produção: simula a baixa FEFO e mostra o que será descontado, sem gravar nada.
        /// </summary>
        /// <remarks>
        /// Uma linha por lote usado (ingrediente que passa por dois lotes vira duas linhas), com necessário, disponível,
        /// lote, validade e quanto sai daquele lote. A lista <c>problemas</c> traz FALTA (insumo, quantidade que falta e
        /// unidade) e VENCIDO (insumo, lote e validade). <c>podeConfirmar</c> é false quando há qualquer problema.
        /// É o mesmo cálculo da confirmação da produção.
        /// </remarks>
        /// <param name="id">Id da receita.</param>
        /// <param name="quantidade">Quantidade a produzir, em litros (maior que zero). Use ponto nos decimais, ex.: 50.5.</param>
        [HttpGet("{id:long}/previa")]
        [ProducesResponseType(typeof(PreviaProducaoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<PreviaProducaoResponse>> Previa(
            long id, [FromQuery] decimal? quantidade, CancellationToken ct) =>
            Ok(await _previa.PreverAsync(id, quantidade, ct));

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
