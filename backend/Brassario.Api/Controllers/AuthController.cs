using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;

namespace Brassario.Api.Controllers
{

    [ApiController]
    [Route("api/v1/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IRecuperacaoSenhaService _recuperacao;
        private readonly JwtOptions _opcoes;

        public AuthController(IAuthService auth, IRecuperacaoSenhaService recuperacao, IOptions<JwtOptions> opcoes)
        {
            _auth = auth;
            _recuperacao = recuperacao;
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
        [PermitirComTrocaPendente]
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
        [PermitirComTrocaPendente]
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

        /// <summary>Devolve a conta de quem está logado, incluindo o e-mail de recuperação (só o próprio usuário vê).</summary>
        [HttpGet("me")]
        [Authorize]
        [PermitirComTrocaPendente]
        [ProducesResponseType(typeof(MinhaContaResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<MinhaContaResponse>> Me(CancellationToken ct)
        {
            var id = User.ObterUsuarioId();
            if (id is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var conta = await _auth.ObterMinhaContaAsync(id.Value, ct);
            return conta is null
                ? Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."))
                : Ok(conta);
        }


        /// <summary>
        /// Primeiro acesso: troca a senha temporária por uma senha nova e, se informado, cadastra o e-mail de
        /// recuperação (que recebe um link de confirmação). Devolve uma sessão nova e o cookie do refresh token.
        /// </summary>
        [HttpPost("primeiro-acesso")]
        [Authorize]
        [PermitirComTrocaPendente]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<LoginResponse>> PrimeiroAcesso([FromBody] PrimeiroAcessoRequest request, CancellationToken ct)
        {
            var id = User.ObterUsuarioId();
            if (id is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var sessao = await _auth.ConcluirPrimeiroAcessoAsync(id.Value, request, ct);
            if (sessao is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            GravarCookie(sessao);
            return Ok(sessao.Resposta);
        }

        /// <summary>Confirma o e-mail de recuperação a partir do link enviado por e-mail (público).</summary>
        [HttpPost("confirmar-email")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ConfirmarEmail([FromBody] ConfirmarEmailRequest request, CancellationToken ct)
        {
            var ok = await _auth.ConfirmarEmailAsync(request.Token, ct);
            return ok
                ? NoContent()
                : BadRequest(new ErroResponse(400, "TOKEN_INVALIDO", "Link inválido ou expirado."));
        }

        /// <summary>
        /// Pede o link para redefinir a senha. Responde sempre 202 com a mesma mensagem, exista a conta ou não.
        /// Limite de 3 pedidos por hora por e-mail de login e por IP.
        /// </summary>
        [HttpPost("esqueci-senha")]
        [AllowAnonymous]
        [EnableRateLimiting("esqueci-senha")]
        [ProducesResponseType(typeof(MensagemResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> EsqueciSenha([FromBody] EsqueciSenhaRequest request, CancellationToken ct)
        {
            // Piso de tempo: conta existente (que envia e-mail) e inexistente demoram o mesmo na resposta
            var piso = Task.Delay(TimeSpan.FromMilliseconds(500), ct);

            await _recuperacao.SolicitarAsync(request.Email, ct);
            await piso;

            return Accepted(new MensagemResponse(
                "Se existir uma conta com esse e-mail e com e-mail de recuperação confirmado, enviamos um link para redefinir a senha."));
        }

        /// <summary>Define a senha nova a partir do link enviado por e-mail (público). Revoga todas as sessões do usuário.</summary>
        [HttpPost("redefinir-senha")]
        [AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RedefinirSenha([FromBody] RedefinirSenhaRequest request, CancellationToken ct)
        {
            var ok = await _recuperacao.RedefinirAsync(request, ct);
            return ok
                ? NoContent()
                : BadRequest(new ErroResponse(400, "TOKEN_INVALIDO", "Link inválido ou expirado."));
        }

        /// <summary>
        /// Troca a senha do usuário logado. Confere a senha atual (errada: 422 SENHA_ATUAL_INCORRETA) e encerra
        /// as sessões dos outros aparelhos; a sessão atual continua.
        /// </summary>
        [HttpPost("alterar-senha")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> AlterarSenha([FromBody] AlterarSenhaRequest request, CancellationToken ct)
        {
            var id = User.ObterUsuarioId();
            if (id is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var ok = await _auth.AlterarSenhaAsync(id.Value, request, Request.Cookies[_opcoes.CookieNome], ct);
            return ok
                ? NoContent()
                : Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));
        }

        /// <summary>
        /// Troca o e-mail de recuperação do usuário logado: zera a confirmação e envia o link para o endereço novo.
        /// O endereço antigo deixa de valer na hora; a recuperação volta quando o novo for confirmado.
        /// </summary>
        [HttpPut("email-recuperacao")]
        [Authorize]
        [ProducesResponseType(typeof(MensagemResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> AlterarEmailRecuperacao([FromBody] AlterarEmailRecuperacaoRequest request, CancellationToken ct)
        {
            var id = User.ObterUsuarioId();
            if (id is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var ok = await _auth.AlterarEmailRecuperacaoAsync(id.Value, request, ct);
            return ok
                ? Accepted(new MensagemResponse("Enviamos um link de confirmação para o novo e-mail. A recuperação de senha volta a funcionar quando você confirmar."))
                : Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));
        }

        /// <summary>
        /// Reenvia o link de confirmação do e-mail de recuperação atual (sem corpo). Invalida os links pendentes.
        /// Sem e-mail cadastrado: 422 SEM_EMAIL_RECUPERACAO. Já confirmado: 422 EMAIL_JA_CONFIRMADO.
        /// Limite de 3 por hora por usuário (429 MUITAS_TENTATIVAS).
        /// </summary>
        [HttpPost("reenviar-confirmacao")]
        [Authorize]
        [ProducesResponseType(typeof(MensagemResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status422UnprocessableEntity)]
        [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status429TooManyRequests)]
        public async Task<IActionResult> ReenviarConfirmacao(CancellationToken ct)
        {
            var id = User.ObterUsuarioId();
            if (id is null)
                return Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));

            var ok = await _recuperacao.ReenviarConfirmacaoAsync(id.Value, ct);
            return ok
                ? Accepted(new MensagemResponse("Enviamos um novo link de confirmação para o seu e-mail de recuperação."))
                : Unauthorized(new ErroResponse(401, "NAO_AUTENTICADO", "Sessão inválida."));
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
