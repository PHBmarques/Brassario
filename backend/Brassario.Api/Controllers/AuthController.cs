using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Brassario.Api.Controllers
{

    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly JwtOptions _opcoes;

        public AuthController(IAuthService auth, IOptions<JwtOptions> opcoes)
        {
            _auth = auth;
            _opcoes = opcoes.Value;
        }

        /// <summary>Valida e-mail e senha e devolve o token de acesso (30 min). O refresh token vai em cookie HttpOnly.</summary>
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var sessao = await _auth.IniciarSessaoAsync(request, ct);
            if (sessao is null)
                return Unauthorized(new ErroResponse(401, "CREDENCIAIS_INVALIDAS", "E-mail ou senha inválidos."));

            GravarCookie(sessao);
            return Ok(sessao.Resposta);
        }

        /// <summary>
        /// Renova a sessão a partir do cookie do refresh token (sem corpo). O token usado é revogado e um novo
        /// cookie é emitido (rotação). Devolve o mesmo formato do login.
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken ct)
        {
            var sessao = await _auth.RenovarSessaoAsync(Request.Cookies[_opcoes.CookieNome], ct);
            if (sessao is null)
            {
                ApagarCookie();
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão expirada. Entre novamente."));
            }

            GravarCookie(sessao);
            return Ok(sessao.Resposta);
        }

        /// <summary>Encerra a sessão: revoga o refresh token do cookie e apaga o cookie.</summary>
        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var usuarioId = User.ObterUsuarioId();
            if (usuarioId is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            await _auth.EncerrarSessaoAsync(usuarioId.Value, Request.Cookies[_opcoes.CookieNome], ct);
            ApagarCookie();
            return NoContent();
        }

        /// <summary>Devolve o usuário dono do token.</summary>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UsuarioResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UsuarioResponse>> Me(CancellationToken ct)
        {
            var id = User.ObterUsuarioId();
            if (id is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var usuario = await _auth.ObterUsuarioAsync(id.Value, ct);
            return usuario is null
                ? Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."))
                : Ok(usuario);
        }

        // ---------- Cookie do refresh token ----------

        // Path "/" porque a API fica atrás de proxy (Vite e Nginx) e o caminho visto pelo navegador pode variar.
        // O risco continua baixo: o cookie é HttpOnly (JavaScript não lê) e SameSite=Strict (não vai em requisição de outro site).
        private CookieOptions OpcoesCookie(DateTime? expiraEm) => new()
        {
            HttpOnly = true,
            Secure = _opcoes.CookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Expires = expiraEm.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(expiraEm.Value, DateTimeKind.Utc)) : null,
            IsEssential = true
        };

        private void GravarCookie(SessaoEmitida sessao) =>
            Response.Cookies.Append(_opcoes.CookieNome, sessao.RefreshToken, OpcoesCookie(sessao.RefreshExpiraEm));

        private void ApagarCookie() =>
            Response.Cookies.Delete(_opcoes.CookieNome, OpcoesCookie(null));
    }
}
