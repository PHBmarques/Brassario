using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;
using Brassario.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Brassario.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher _hasher;
        private readonly IJwtService _jwt;
        private readonly JwtOptions _opcoes;
        private readonly IRelogio _relogio;

        public AuthService(AppDbContext db, IPasswordHasher hasher, IJwtService jwt, IOptions<JwtOptions> opcoes, IRelogio relogio)
        {
            _db = db;
            _hasher = hasher;
            _jwt = jwt;
            _opcoes = opcoes.Value;
            _relogio = relogio;
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
