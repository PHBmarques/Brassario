using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{
    [ApiController]
    [Route("api/v1/entradas")]
    [Produces("application/json")]
    [Authorize]
    public class EntradasController : ControllerBase
    {
        private readonly IEntradaService _entradas;

        public EntradasController(IEntradaService entradas) => _entradas = entradas;

        /// <summary>Registra a entrada de estoque, com insumo e/ou fornecedor novos, tudo em uma transação.</summary>
        /// <remarks>
        /// Para o insumo, informe `insumoId` (já existe) **ou** `novoInsumo` (dados do cadastro), nunca os dois e nunca nenhum.
        /// O fornecedor é opcional: `fornecedorId`, `novoFornecedor` ou nenhum dos dois (nunca os dois juntos).
        ///
        /// Cria o insumo novo, o fornecedor novo, o lote e a movimentação ENTRADA do usuário logado na mesma transação:
        /// se qualquer parte falhar, nada é gravado. Se `lote.codigoLote` vier vazio, o sistema gera `L-AAAAMMDD-NN`.
        ///
        /// Erros por campo vêm em `detalhes`, com o caminho do campo (`lote.quantidade`, `novoInsumo.nome`,
        /// `novoFornecedor.nome`). Nome repetido devolve 409 `NOME_DUPLICADO`; código de lote repetido, 409 `CODIGO_LOTE_DUPLICADO`.
        /// Permitido para ADMIN e OPERADOR.
        /// </remarks>
        [HttpPost]
        [ProducesResponseType(typeof(EntradaResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<EntradaResponse>> Registrar([FromBody] EntradaRequest request, CancellationToken ct)
        {
            var usuarioId = User.ObterUsuarioId();
            if (usuarioId is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var entrada = await _entradas.RegistrarAsync(request, usuarioId.Value, ct);
            return StatusCode(StatusCodes.Status201Created, entrada);
        }
    }
}
