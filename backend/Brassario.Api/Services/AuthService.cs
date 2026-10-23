using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;
using Brassario.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Brassario.Api.Exceptions;
using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IJwtService _jwt;
        private readonly JwtOptions _opcoes;
        private readonly IRelogio _relogio;

        private readonly IEmailService _email;
        private readonly AppOptions _app;

        public AuthService(AppDbContext db, IPasswordHasher hasher, IJwtService jwt, IOptions<JwtOptions> opcoes,
            IRelogio relogio, IEmailService email, IOptions<AppOptions> app)
        {
            _db = db;
            _hasher = hasher;
            _jwt = jwt;
            _opcoes = opcoes.Value;
            _relogio = relogio;
            _email = email;
            _app = app.Value;
        }

        public async Task<SessaoEmitida?> IniciarSessaoAsync(LoginRequest request, CancellationToken ct = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

            // Mesma resposta para e-mail inexistente, senha errada e usuário inativo (não revela qual foi)
            if (usuario is null || !usuario.Ativo || !_hasher.Verify(request.Senha, usuario.SenhaHash))
                return null;

            return await EmitirSessaoAsync(usuario, ct);
        }

        public async Task<SessaoEmitida?> RenovarSessaoAsync(string? refreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return null;

            var hash = RefreshTokenHelper.Hash(refreshToken);
            var agora = _relogio.AgoraUtc;

            var atual = await _db.RefreshTokens
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

            // Não existe, já foi usado/revogado ou venceu: sessão inválida
            if (atual is null || atual.Revogado || atual.ExpiraEm <= agora)
                return null;

            // Usuário inativado depois do login: a sessão deixa de valer
            if (!atual.Usuario.Ativo)
            {
                atual.Revogado = true;
                await _db.SaveChangesAsync(ct);
                return null;
            }

            // Rotação: o token usado é revogado e um novo é emitido, na mesma gravação
            atual.Revogado = true;
            return await EmitirSessaoAsync(atual.Usuario, ct);
        }

        public async Task EncerrarSessaoAsync(long usuarioId, string? refreshToken, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
                return;

            var hash = RefreshTokenHelper.Hash(refreshToken);

            // Só revoga o token do próprio usuário logado
            var token = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsuarioId == usuarioId, ct);

            if (token is null || token.Revogado)
                return;

            token.Revogado = true;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<UsuarioResponse?> ObterUsuarioAsync(long id, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.Ativo, ct);
            return usuario is null ? null : UsuarioResponse.De(usuario);
        }


        public async Task<SessaoEmitida?> ConcluirPrimeiroAcessoAsync(long usuarioId, PrimeiroAcessoRequest request, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct);
            if (usuario is null)
                return null;

            // Sem isso, qualquer usuário logado trocaria a senha sem informar a atual
            if (!usuario.DeveTrocarSenha)
                throw new NegocioException("A troca de senha não está pendente para esta conta.", "TROCA_NAO_PENDENTE");

            // ---- Valida tudo e devolve todos os erros juntos (400) ----
            var erros = new Dictionary<string, string[]>();

            var problemasSenha = PasswordPolicy.Validar(request.NovaSenha).ToList();
            if (problemasSenha.Count == 0 && _hasher.Verify(request.NovaSenha, usuario.SenhaHash))
                problemasSenha.Add("A nova senha deve ser diferente da senha temporária.");
            if (problemasSenha.Count > 0)
                erros["novaSenha"] = problemasSenha.ToArray();

            if (request.NovaSenha != request.ConfirmarSenha)
                erros["confirmarSenha"] = new[] { "A confirmação deve ser igual à nova senha." };

            string? emailRecuperacao = null;
            if (!string.IsNullOrWhiteSpace(request.EmailRecuperacao))
            {
                emailRecuperacao = request.EmailRecuperacao.Trim().ToLowerInvariant();
                var problemaEmail = ValidarEmailRecuperacao(emailRecuperacao, usuario);
                if (problemaEmail is not null)
                    erros["emailRecuperacao"] = new[] { problemaEmail };
            }

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            // ---- Grava ----
            var agora = _relogio.AgoraUtc;

            usuario.SenhaHash = _hasher.Hash(request.NovaSenha);   // BCrypt: a senha em si nunca é gravada
            usuario.DeveTrocarSenha = false;

            string? tokenConfirmacao = null;
            if (emailRecuperacao is not null)
                tokenConfirmacao = await PrepararConfirmacaoEmailAsync(usuario, emailRecuperacao, agora, ct);

            // Senha nova: as sessões abertas (a da senha temporária) deixam de valer
            await RevogarSessoesAsync(usuario.Id, ct);

            // Emite a sessão nova (sem a claim pwd_change, porque DeveTrocarSenha agora é false).
            // O SaveChanges de dentro grava senha, token e revogações na mesma operação.
            var sessao = await EmitirSessaoAsync(usuario, ct);

            if (tokenConfirmacao is not null)
                await EnviarConfirmacaoEmailAsync(usuario, tokenConfirmacao, ct);

            return sessao;
        }

        public async Task<bool> ConfirmarEmailAsync(string? token, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var hash = RefreshTokenHelper.Hash(token.Trim());
            var agora = _relogio.AgoraUtc;

            var registro = await _db.TokensUsuario
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Tipo == TipoTokenUsuario.CONFIRMAR_EMAIL, ct);

            // Inexistente, já usado, vencido ou de conta inativa: mesma resposta para todos
            if (registro is null || registro.UsadoEm is not null || registro.ExpiraEm <= agora || !registro.Usuario.Ativo)
                return false;

            // E-mail trocado depois do pedido: o link antigo não vale mais
            if (!string.Equals(registro.EmailDestino, registro.Usuario.EmailRecuperacao, StringComparison.OrdinalIgnoreCase))
                return false;

            registro.UsadoEm = agora;
            registro.Usuario.EmailRecuperacaoConfirmadoEm = agora;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task SolicitarRedefinicaoSenhaAsync(string? email, CancellationToken ct = default)
        {
            var informado = (email ?? string.Empty).Trim().ToLowerInvariant();
            if (informado.Length == 0 || informado.Length > 160)
                return;

            // Vale o e-mail de login ou o de recuperação, desde que a recuperação esteja confirmada
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u =>
                u.Ativo && u.EmailRecuperacaoConfirmadoEm != null && u.EmailRecuperacao != null &&
                (u.Email.ToLower() == informado || u.EmailRecuperacao.ToLower() == informado), ct);

            // Sem conta, inativa ou sem e-mail confirmado: não faz nada (a resposta é a mesma, para não revelar contas)
            if (usuario is null)
                return;

            var agora = _relogio.AgoraUtc;

            // Só o link mais recente vale
            await InvalidarTokensAsync(usuario.Id, TipoTokenUsuario.REDEFINIR_SENHA, agora, ct);

            var token = RefreshTokenHelper.Gerar();
            _db.TokensUsuario.Add(new TokenUsuario
            {
                UsuarioId = usuario.Id,
                Tipo = TipoTokenUsuario.REDEFINIR_SENHA,
                TokenHash = RefreshTokenHelper.Hash(token),   // só o hash vai para o banco
                EmailDestino = usuario.EmailRecuperacao,
                ExpiraEm = agora.AddHours(1),
                CriadoEm = agora
            });
            await _db.SaveChangesAsync(ct);

            var link = $"{_app.UrlFront.TrimEnd('/')}/redefinir-senha?token={Uri.EscapeDataString(token)}";
            await _email.EnviarAsync(usuario.EmailRecuperacao!, "Redefinição de senha do Brassário",
                EmailModelos.RedefinirSenha(usuario.Nome, link), ct);
        }

        public async Task<bool> RedefinirSenhaAsync(RedefinirSenhaRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return false;

            var hash = RefreshTokenHelper.Hash(request.Token.Trim());
            var agora = _relogio.AgoraUtc;

            var registro = await _db.TokensUsuario
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Tipo == TipoTokenUsuario.REDEFINIR_SENHA, ct);

            // Inexistente, já usado, vencido ou de conta inativa: mesma resposta para todos
            if (registro is null || registro.UsadoEm is not null || registro.ExpiraEm <= agora || !registro.Usuario.Ativo)
                return false;

            var erros = ValidarNovaSenha(request.NovaSenha, request.ConfirmarSenha, null);
            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            var usuario = registro.Usuario;
            usuario.SenhaHash = _hasher.Hash(request.NovaSenha);
            usuario.DeveTrocarSenha = false;   // a pessoa escolheu a senha: não é mais temporária
            registro.UsadoEm = agora;

            // A sessão pode estar aberta em outro aparelho: todas deixam de valer
            await RevogarSessoesAsync(usuario.Id, ct);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<SessaoEmitida?> AlterarSenhaAsync(long usuarioId, AlterarSenhaRequest request, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct);
            if (usuario is null)
                return null;

            var erros = new Dictionary<string, string[]>();
            var atualConfere = !string.IsNullOrEmpty(request.SenhaAtual) && _hasher.Verify(request.SenhaAtual, usuario.SenhaHash);
            if (!atualConfere)
                erros["senhaAtual"] = new[] { string.IsNullOrEmpty(request.SenhaAtual) ? "Informe a senha atual." : "A senha atual não confere." };

            foreach (var (campo, problemas) in ValidarNovaSenha(request.NovaSenha, request.ConfirmarSenha, atualConfere ? usuario : null))
                erros[campo] = problemas;

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            usuario.SenhaHash = _hasher.Hash(request.NovaSenha);
            usuario.DeveTrocarSenha = false;

            // Encerra as sessões dos outros aparelhos; esta recebe uma sessão nova
            await RevogarSessoesAsync(usuario.Id, ct);
            return await EmitirSessaoAsync(usuario, ct);
        }

        public async Task<UsuarioResponse?> AlterarEmailRecuperacaoAsync(long usuarioId, EmailRecuperacaoRequest request, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct);
            if (usuario is null)
                return null;

            var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
            var problema = email.Length == 0 ? "Informe o e-mail de recuperação." : ValidarEmailRecuperacao(email, usuario);
            if (problema is not null)
                throw new ValidacaoException("email", problema);

            var token = await PrepararConfirmacaoEmailAsync(usuario, email, _relogio.AgoraUtc, ct);
            await _db.SaveChangesAsync(ct);
            await EnviarConfirmacaoEmailAsync(usuario, token, ct);

            return UsuarioResponse.De(usuario);
        }

        // ---------- Apoio ----------

        private static string? ValidarEmailRecuperacao(string email, Usuario usuario)
        {
            if (email.Length > 160)
                return "O e-mail deve ter no máximo 160 caracteres.";
            if (!new EmailAddressAttribute().IsValid(email))
                return "Informe um e-mail válido.";
            if (string.Equals(email, usuario.Email, StringComparison.OrdinalIgnoreCase))
                return "O e-mail de recuperação deve ser diferente do e-mail de login.";
            return null;
        }

        // Regras da senha nova + confirmação. Com "atual", a senha nova também precisa ser diferente da atual.
        private Dictionary<string, string[]> ValidarNovaSenha(string? novaSenha, string? confirmarSenha, Usuario? atual)
        {
            var erros = new Dictionary<string, string[]>();

            var problemas = PasswordPolicy.Validar(novaSenha).ToList();
            if (problemas.Count == 0 && atual is not null && _hasher.Verify(novaSenha!, atual.SenhaHash))
                problemas.Add("A nova senha deve ser diferente da senha atual.");
            if (problemas.Count > 0)
                erros["novaSenha"] = problemas.ToArray();

            if (novaSenha != confirmarSenha)
                erros["confirmarSenha"] = new[] { "A confirmação deve ser igual à nova senha." };

            return erros;
        }

        // Grava o e-mail (ainda não confirmado) e cria o token de confirmação. Devolve o token para o link.
        private async Task<string> PrepararConfirmacaoEmailAsync(Usuario usuario, string email, DateTime agora, CancellationToken ct)
        {
            usuario.EmailRecuperacao = email;
            usuario.EmailRecuperacaoConfirmadoEm = null;

            // Links de confirmação e de redefinição anteriores deixam de valer
            await InvalidarTokensAsync(usuario.Id, TipoTokenUsuario.CONFIRMAR_EMAIL, agora, ct);
            await InvalidarTokensAsync(usuario.Id, TipoTokenUsuario.REDEFINIR_SENHA, agora, ct);

            var token = RefreshTokenHelper.Gerar();
            _db.TokensUsuario.Add(new TokenUsuario
            {
                UsuarioId = usuario.Id,
                Tipo = TipoTokenUsuario.CONFIRMAR_EMAIL,
                TokenHash = RefreshTokenHelper.Hash(token),   // só o hash vai para o banco
                EmailDestino = email,
                ExpiraEm = agora.AddHours(24),
                CriadoEm = agora
            });
            return token;
        }

        // O e-mail só sai depois de gravar. Falha no envio é registrada pelo serviço e não derruba a requisição.
        private Task EnviarConfirmacaoEmailAsync(Usuario usuario, string token, CancellationToken ct)
        {
            var link = $"{_app.UrlFront.TrimEnd('/')}/confirmar-email?token={Uri.EscapeDataString(token)}";
            return _email.EnviarAsync(usuario.EmailRecuperacao!, "Confirme seu e-mail de recuperação",
                EmailModelos.ConfirmarEmail(usuario.Nome, link), ct);
        }

        private async Task InvalidarTokensAsync(long usuarioId, TipoTokenUsuario tipo, DateTime agora, CancellationToken ct)
        {
            var pendentes = await _db.TokensUsuario
                .Where(t => t.UsuarioId == usuarioId && t.Tipo == tipo && t.UsadoEm == null)
                .ToListAsync(ct);
            foreach (var p in pendentes)
                p.UsadoEm = agora;
        }

        private async Task RevogarSessoesAsync(long usuarioId, CancellationToken ct)
        {
            var abertos = await _db.RefreshTokens.Where(t => t.UsuarioId == usuarioId && !t.Revogado).ToListAsync(ct);
            foreach (var t in abertos)
                t.Revogado = true;
        }

        private async Task<SessaoEmitida> EmitirSessaoAsync(Usuario usuario, CancellationToken ct)
        {
            var agora = _relogio.AgoraUtc;

            // Limpeza: tokens deste usuário que já venceram não servem para nada
            var vencidos = await _db.RefreshTokens
                .Where(t => t.UsuarioId == usuario.Id && t.ExpiraEm <= agora)
                .ToListAsync(ct);
            _db.RefreshTokens.RemoveRange(vencidos);

            // O token vai para o cookie; no banco fica só o hash
            var refreshToken = RefreshTokenHelper.Gerar();
            var expiraEm = agora.AddDays(_opcoes.RefreshDays);

            _db.RefreshTokens.Add(new RefreshToken
            {
                UsuarioId = usuario.Id,
                TokenHash = RefreshTokenHelper.Hash(refreshToken),
                ExpiraEm = expiraEm,
                Revogado = false
            });

            // Grava o novo token e a revogação do antigo (se houver) juntos
            await _db.SaveChangesAsync(ct);

            var (accessToken, _) = _jwt.GerarAccessToken(usuario);
            var resposta = new LoginResponse(accessToken, "Bearer", _opcoes.ExpiresMinutes * 60, UsuarioResponse.De(usuario));
            return new SessaoEmitida(resposta, refreshToken, expiraEm);
        }
    }
}
