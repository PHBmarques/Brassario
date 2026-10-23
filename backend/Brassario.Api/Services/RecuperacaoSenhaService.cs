using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Brassario.Api.Services
{
    public class RecuperacaoSenhaService : IRecuperacaoSenhaService
    {
        private static readonly TimeSpan JanelaLimite = TimeSpan.FromHours(1);
        private static readonly TimeSpan ValidadeToken = TimeSpan.FromMinutes(30);

        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IRelogio _relogio;
        private readonly IEmailService _email;
        private readonly ILimitadorTentativas _limitador;
        private readonly AppOptions _app;
        private readonly SegurancaOptions _seguranca;
        private readonly ILogger<RecuperacaoSenhaService> _log;

        public RecuperacaoSenhaService(AppDbContext db, IPasswordHasher hasher, IRelogio relogio, IEmailService email,
            ILimitadorTentativas limitador, IOptions<AppOptions> app, IOptions<SegurancaOptions> seguranca,
            ILogger<RecuperacaoSenhaService> log)
        {
            _db = db;
            _hasher = hasher;
            _relogio = relogio;
            _email = email;
            _limitador = limitador;
            _app = app.Value;
            _seguranca = seguranca.Value;
            _log = log;
        }

        public async Task SolicitarAsync(string? emailLogin, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(emailLogin))
                throw new ValidacaoException("email", "Informe o e-mail.");

            var email = emailLogin.Trim().ToLowerInvariant();

            // Conta todo pedido, exista a conta ou não: o 429 não revela quem tem cadastro
            if (!_limitador.Tentar($"esqueci-senha:{email}", _seguranca.EsqueciSenhaPorHora, JanelaLimite))
                throw new MuitasTentativasException();

            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

            // Mesmo comportamento para conta inexistente, inativa, sem e-mail de recuperação ou com e-mail não confirmado
            if (usuario is null || !usuario.Ativo
                || string.IsNullOrWhiteSpace(usuario.EmailRecuperacao)
                || usuario.EmailRecuperacaoConfirmadoEm is null)
            {
                _log.LogInformation("Pedido de redefinição de senha sem envio.");
                return;
            }

            var agora = _relogio.AgoraUtc;

            // Links anteriores deixam de valer
            var pendentes = await _db.TokensUsuario
                .Where(t => t.UsuarioId == usuario.Id && t.Tipo == TipoTokenUsuario.REDEFINIR_SENHA && t.UsadoEm == null)
                .ToListAsync(ct);
            foreach (var p in pendentes)
                p.UsadoEm = agora;

            var token = RefreshTokenHelper.Gerar();
            _db.TokensUsuario.Add(new TokenUsuario
            {
                UsuarioId = usuario.Id,
                Tipo = TipoTokenUsuario.REDEFINIR_SENHA,
                TokenHash = RefreshTokenHelper.Hash(token),   // só o hash vai para o banco
                EmailDestino = usuario.EmailRecuperacao,
                ExpiraEm = agora.Add(ValidadeToken),
                CriadoEm = agora
            });
            await _db.SaveChangesAsync(ct);

            var link = $"{_app.UrlFront.TrimEnd('/')}/redefinir-senha?token={Uri.EscapeDataString(token)}";
            var enviado = await _email.EnviarAsync(usuario.EmailRecuperacao, "Redefinição de senha do Brassário",
                EmailModelos.RedefinirSenha(usuario.Nome, usuario.Email, link), ct);

            // Só o id do usuário: nunca o token, o link nem o endereço
            if (enviado)
                _log.LogInformation("Link de redefinição de senha enviado ao usuário {UsuarioId}.", usuario.Id);
            else
                _log.LogWarning("Falha ao enviar o link de redefinição de senha ao usuário {UsuarioId}.", usuario.Id);
        }

        public async Task<bool> RedefinirAsync(RedefinirSenhaRequest request, CancellationToken ct = default)
        {
            // Valida os campos antes de olhar o token, para uma senha fraca não gastar o link
            var erros = new Dictionary<string, string[]>();

            var problemasSenha = PasswordPolicy.Validar(request.NovaSenha);
            if (problemasSenha.Count > 0)
                erros["novaSenha"] = problemasSenha.ToArray();

            if (request.NovaSenha != request.ConfirmarSenha)
                erros["confirmarSenha"] = new[] { "A confirmação deve ser igual à nova senha." };

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            if (string.IsNullOrWhiteSpace(request.Token))
                return false;

            var hash = RefreshTokenHelper.Hash(request.Token.Trim());
            var agora = _relogio.AgoraUtc;

            var registro = await _db.TokensUsuario
                .Include(t => t.Usuario)
                .FirstOrDefaultAsync(t => t.TokenHash == hash && t.Tipo == TipoTokenUsuario.REDEFINIR_SENHA, ct);

            // Inexistente, usado, vencido ou de conta inativa: mesma resposta para todos
            if (registro is null || registro.UsadoEm is not null || registro.ExpiraEm <= agora || !registro.Usuario.Ativo)
                return false;

            var usuario = registro.Usuario;

            // O e-mail de recuperação mudou ou deixou de estar confirmado depois do pedido: o link não vale mais
            if (usuario.EmailRecuperacaoConfirmadoEm is null
                || !string.Equals(registro.EmailDestino, usuario.EmailRecuperacao, StringComparison.OrdinalIgnoreCase))
                return false;

            usuario.SenhaHash = _hasher.Hash(request.NovaSenha);   // BCrypt: a senha em si nunca é gravada
            usuario.DeveTrocarSenha = false;
            registro.UsadoEm = agora;

            // Qualquer outro link pendente também deixa de valer
            var outros = await _db.TokensUsuario
                .Where(t => t.UsuarioId == usuario.Id && t.Tipo == TipoTokenUsuario.REDEFINIR_SENHA && t.UsadoEm == null)
                .ToListAsync(ct);
            foreach (var o in outros)
                o.UsadoEm = agora;

            // Todas as sessões abertas deixam de valer
            var sessoes = await _db.RefreshTokens.Where(t => t.UsuarioId == usuario.Id && !t.Revogado).ToListAsync(ct);
            foreach (var s in sessoes)
                s.Revogado = true;

            await _db.SaveChangesAsync(ct);

            _log.LogInformation("Senha redefinida pelo link para o usuário {UsuarioId}.", usuario.Id);
            return true;
        }
        public async Task<bool> ReenviarConfirmacaoAsync(long usuarioId, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId && u.Ativo, ct);
            if (usuario is null)
                return false;

            if (string.IsNullOrWhiteSpace(usuario.EmailRecuperacao))
                throw new NegocioException("Você ainda não cadastrou um e-mail de recuperação.", "SEM_EMAIL_RECUPERACAO");

            if (usuario.EmailRecuperacaoConfirmadoEm is not null)
                throw new NegocioException("O seu e-mail de recuperação já está confirmado.", "EMAIL_JA_CONFIRMADO");

            // Só conta quando o link vai de fato ser enviado: os erros acima não gastam as tentativas
            if (!_limitador.Tentar($"reenviar-confirmacao:{usuario.Id}", _seguranca.ReenviarConfirmacaoPorHora, JanelaLimite))
                throw new MuitasTentativasException();

            var agora = _relogio.AgoraUtc;

            // Links anteriores deixam de valer
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
                EmailDestino = usuario.EmailRecuperacao,
                ExpiraEm = agora.AddHours(24),
                CriadoEm = agora
            });
            await _db.SaveChangesAsync(ct);

            var link = $"{_app.UrlFront.TrimEnd('/')}/confirmar-email?token={Uri.EscapeDataString(token)}";
            var enviado = await _email.EnviarAsync(usuario.EmailRecuperacao, "Confirme seu e-mail de recuperação",
                EmailModelos.ConfirmarEmail(usuario.Nome, link), ct);

            // Só o id do usuário: nunca o token, o link nem o endereço
            if (enviado)
                _log.LogInformation("Link de confirmação reenviado ao usuário {UsuarioId}.", usuario.Id);
            else
                _log.LogWarning("Falha ao reenviar o link de confirmação ao usuário {UsuarioId}.", usuario.Id);

            return true;
        }
    }
}