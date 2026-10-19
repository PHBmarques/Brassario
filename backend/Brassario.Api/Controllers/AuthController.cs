using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brassario.Api.Controllers
{

    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;

        public AuthController(IAuthService auth) => _auth = auth;

        /// <summary>Valida e-mail e senha e devolve o token de acesso (30 min).</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var resposta = await _auth.LoginAsync(request, ct);
            if (resposta is null)
                return Unauthorized(new ErroResponse(401,"CREDENCIAIS_INVALIDAS", "E-mail ou senha inválidos."));

            return Ok(resposta);
        }

        /// <summary>Devolve o usuário dono do token.</summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UsuarioResponse>> Me(CancellationToken ct)
        {
            var sub = User.FindFirst("sub")?.Value;
            if (!long.TryParse(sub, out var id))
                return Unauthorized(new ErroResponse(401,"NAO_AUTENTICADO", "Sessão inválida."));

            var usuario = await _auth.ObterUsuarioAsync(id, ct);
            return usuario is null
                ? Unauthorized(new ErroResponse(401,"NAO_AUTENTICADO", "Sessão inválida."))
                : Ok(usuario);
        }
    }
}
