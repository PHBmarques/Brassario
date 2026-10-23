using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{

    [ApiController]
    [Route("api/v1/usuarios")]
    [Produces("application/json")]
    [Authorize(Roles = "ADMIN")]
    public class UsuariosController : ControllerBase
    {
        private readonly IUsuarioService _usuarios;

        public UsuariosController(IUsuarioService usuarios) => _usuarios = usuarios;

        /// <summary>Lista os usuários em ordem de nome, com busca opcional por nome ou e-mail.</summary>
        /// <param name="busca">Parte do nome ou do e-mail.</param>
        /// <param name="incluirInativos">Padrão true, para o ADMIN poder reativar. Use false para ver só os ativos.</param>
        [HttpGet]
        [ProducesResponseType(typeof(List<UsuarioResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<UsuarioResponse>>> Listar(
            [FromQuery] string? busca, [FromQuery] bool incluirInativos = true, CancellationToken ct = default) =>
            Ok(await _usuarios.ListarAsync(busca, incluirInativos, ct));

        /// <summary>Busca um usuário pelo id.</summary>
        [HttpGet("{id:long}")]
        [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UsuarioResponse>> ObterPorId(long id, CancellationToken ct) =>
            Ok(await _usuarios.ObterAsync(id, ct));

        /// <summary>Cadastra um usuário. O e-mail não pode repetir; a senha é guardada como hash BCrypt.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        public async Task<ActionResult<UsuarioResponse>> Criar([FromBody] UsuarioRequest request, CancellationToken ct)
        {
            var criado = await _usuarios.CriarAsync(request, ct);
            return CreatedAtAction(nameof(ObterPorId), new { id = criado.Id }, criado);
        }

        /// <summary>Edita nome, e-mail e perfil. Se a senha for informada, ela é trocada e as sessões abertas do usuário são encerradas.</summary>
        [HttpPut("{id:long}")]
        [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<UsuarioResponse>> Atualizar(long id, [FromBody] UsuarioAtualizarRequest request, CancellationToken ct) =>
            Ok(await _usuarios.AtualizarAsync(id, request, ct));

        /// <summary>Ativa ou inativa um usuário. Não existe exclusão (RN-16). Não dá para inativar a si mesmo nem o último ADMIN ativo.</summary>
        [HttpPatch("{id:long}/status")]
        [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<UsuarioResponse>> AlterarStatus(long id, [FromBody] AlterarStatusRequest request, CancellationToken ct)
        {
            var logado = User.ObterUsuarioId();
            if (logado is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            return Ok(await _usuarios.AlterarStatusAsync(id, request.Ativo!.Value, logado.Value, ct));
        }

        /// <summary>
        /// Gera uma senha temporária para o usuário (ex.: ele esqueceu a senha e não tem e-mail de recuperação).
        /// A senha só aparece nesta resposta; no próximo acesso ele cria uma senha nova. As sessões abertas são encerradas.
        /// </summary>
        [HttpPost("{id:long}/redefinir-senha")]
        [ProducesResponseType(typeof(SenhaTemporariaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<SenhaTemporariaResponse>> RedefinirSenha(long id, CancellationToken ct)
        {
            var logado = User.ObterUsuarioId();
            if (logado is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            return Ok(await _usuarios.GerarSenhaTemporariaAsync(id, logado.Value, ct));
        }
    }
}
