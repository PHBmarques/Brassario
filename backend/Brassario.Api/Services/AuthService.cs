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
        private readonly IBloqueioDeLogin? _bloqueio;

        public AuthService(AppDbContext db, IPasswordHasher hasher, IJwtService jwt, IOptions<JwtOptions> opcoes,
            IRelogio relogio, IEmailService email, IOptions<AppOptions> app, IBloqueioDeLogin? bloqueio = null)
        {
            _bloqueio = bloqueio;
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

            // E-mail bloqueado por senhas erradas seguidas: nem confere a senha (429 LOGIN_BLOQUEADO)
            if (_bloqueio?.TempoRestante(email) is { } restante)
                throw new LoginBloqueadoException(restante);

            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

            // Mesma resposta para e-mail inexistente, senha errada e usuário inativo (não revela qual foi)
            if (usuario is null || !usuario.Ativo || !_hasher.Verify(request.Senha, usuario.SenhaHash))
            {
                _bloqueio?.RegistrarFalha(email);
                return null;
            }

            _bloqueio?.Zerar(email);
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

        public async Task<MinhaContaResponse?> ObterMinhaContaAsync(long id, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.Ativo, ct);
            return usuario is null ? null : MinhaContaResponse.De(usuario);
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
                if (emailRecuperacao.Length > 160)
                    erros["emailRecuperacao"] = new[] { "O e-mail deve ter no máximo 160 caracteres." };
                else if (!new EmailAddressAttribute().IsValid(emailRecuperacao))
                    erros["emailRecuperacao"] = new[] { "Informe um e-mail válido." };
                else if (string.Equals(emailRecuperacao, usuario.Email, StringComparison.OrdinalIgnoreCase))
                    erros["emailRecuperacao"] = new[] { "O e-mail de recuperação deve ser diferente do e-mail de login." };
            }

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            // ---- Grava ----
            var agora = _relogio.AgoraUtc;

            usuario.SenhaHash = _hasher.Hash(request.NovaSenha);   // BCrypt: a senha em si nunca é gravada
            usuario.DeveTrocarSenha = false;

            string? tokenConfirmacao = null;
            if (emailRecuperacao is not null)
            {
                usuario.EmailRecuperacao = emailRecuperacao;
                usuario.EmailRecuperacaoConfirmadoEm = null;

                // Links de confirmação anteriores deixam de valer
                var pendentes = await _db.TokensUsuario
                    .Where(t => t.UsuarioId == usuario.Id && t.Tipo == TipoTokenUsuario.CONFIRMAR_EMAIL && t.UsadoEm == null)
                    .ToListAsync(ct);
                foreach (var p in pendentes)
                    p.UsadoEm = agora;

                tokenConfirmacao = RefreshTokenHelper.Gerar();
                _db.TokensUsuario.Add(new TokenUsuario
                {
                    UsuarioId = usuario.Id,
                    Tipo = TipoTokenUsuario.CONFIRMAR_EMAIL,
                    TokenHash = RefreshTokenHelper.Hash(tokenConfirmacao),   // só o hash vai para o banco
                    EmailDestino = emailRecuperacao,
                    ExpiraEm = agora.AddHours(24),
                    CriadoEm = agora
                });
            }

            // Senha nova: as sessões abertas (a da senha temporária) deixam de valer
            var abertos = await _db.RefreshTokens.Where(t => t.UsuarioId == usuario.Id && !t.Revogado).ToListAsync(ct);
            foreach (var t in abertos)
                t.Revogado = true;

            // Emite a sessão nova (sem a claim pwd_change, porque DeveTrocarSenha agora é false).
            // O SaveChanges de dentro grava senha, token e revogações na mesma operação.
            var sessao = await EmitirSessaoAsync(usuario, ct);

            // O e-mail só sai depois de gravar. Falha no envio é registrada pelo serviço e não derruba a requisição.
            if (tokenConfirmacao is not null)
            {
                var link = $"{_app.UrlFront.TrimEnd('/')}/confirmar-email?token={Uri.EscapeDataString(tokenConfirmacao)}";
                await _email.EnviarAsync(emailRecuperacao!, "Confirme seu e-mail de recuperação",
                    EmailModelos.ConfirmarEmail(usuario.Nome, link), ct);
            }

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

        public async Task<bool> AlterarSenhaAsync(long usuarioId, AlterarSenhaRequest request, string? refreshTokenAtual, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct);
            if (usuario is null)
                return false;

            // ---- Valida os campos e devolve todos os erros juntos (400) ----
            var erros = new Dictionary<string, string[]>();

            if (string.IsNullOrEmpty(request.SenhaAtual))
                erros["senhaAtual"] = new[] { "Informe a senha atual." };

            var problemasSenha = PasswordPolicy.Validar(request.NovaSenha).ToList();
            if (problemasSenha.Count == 0 && request.NovaSenha == request.SenhaAtual)
                problemasSenha.Add("A nova senha deve ser diferente da senha atual.");
            if (problemasSenha.Count > 0)
                erros["novaSenha"] = problemasSenha.ToArray();

            if (request.NovaSenha != request.ConfirmarSenha)
                erros["confirmarSenha"] = new[] { "A confirmação deve ser igual à nova senha." };

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            // ---- Confere a senha atual (BCrypt) ----
            if (!_hasher.Verify(request.SenhaAtual, usuario.SenhaHash))
                throw new NegocioException("A senha atual está incorreta.", "SENHA_ATUAL_INCORRETA");

            usuario.SenhaHash = _hasher.Hash(request.NovaSenha);   // BCrypt: a senha em si nunca é gravada

            // Os outros aparelhos saem; a sessão de quem acabou de trocar a senha continua
            var hashAtual = string.IsNullOrWhiteSpace(refreshTokenAtual) ? null : RefreshTokenHelper.Hash(refreshTokenAtual);
            var outros = await _db.RefreshTokens
                .Where(t => t.UsuarioId == usuario.Id && !t.Revogado && t.TokenHash != hashAtual)
                .ToListAsync(ct);
            foreach (var t in outros)
                t.Revogado = true;

            await _db.SaveChangesAsync(ct);
            return true;
        }

        public async Task<bool> AlterarEmailRecuperacaoAsync(long usuarioId, AlterarEmailRecuperacaoRequest request, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct);
            if (usuario is null)
                return false;

            // ---- Valida (400) ----
            var email = (request.EmailRecuperacao ?? string.Empty).Trim().ToLowerInvariant();
            string? erro = null;
            if (email.Length == 0)
                erro = "Informe o e-mail de recuperação.";
            else if (email.Length > 160)
                erro = "O e-mail deve ter no máximo 160 caracteres.";
            else if (!new EmailAddressAttribute().IsValid(email))
                erro = "Informe um e-mail válido.";
            else if (string.Equals(email, usuario.Email, StringComparison.OrdinalIgnoreCase))
                erro = "O e-mail de recuperação deve ser diferente do e-mail de login.";
            else if (usuario.EmailRecuperacaoConfirmadoEm is not null
                     && string.Equals(email, usuario.EmailRecuperacao, StringComparison.OrdinalIgnoreCase))
                erro = "Este já é o seu e-mail de recuperação.";

            if (erro is not null)
                throw new ValidacaoException("emailRecuperacao", erro);

            var agora = _relogio.AgoraUtc;

            // Endereço novo, ainda sem confirmação: o antigo deixa de valer na hora
            usuario.EmailRecuperacao = email;
            usuario.EmailRecuperacaoConfirmadoEm = null;

            // Links de confirmação anteriores deixam de valer
            var pendentes = await _db.TokensUsuario
                .Where(t => t.UsuarioId == usuario.Id && t.Tipo == TipoTokenUsuario.CONFIRMAR_EMAIL && t.UsadoEm == null)
                .ToListAsync(ct);
            foreach (var p in pendentes)
                p.UsadoEm = agora;

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
            await _db.SaveChangesAsync(ct);

            // O e-mail só sai depois de gravar. Falha no envio é registrada pelo serviço e não derruba a requisição.
            var link = $"{_app.UrlFront.TrimEnd('/')}/confirmar-email?token={Uri.EscapeDataString(token)}";
            await _email.EnviarAsync(email, "Confirme seu e-mail de recuperação",
                EmailModelos.ConfirmarEmail(usuario.Nome, link), ct);

            return true;
        }
        // ---------- Apoio ----------

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
