using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Security;
using Microsoft.AspNetCore.Identity.Data;
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

        public AuthService(AppDbContext db, IPasswordHasher hasher, IJwtService jwt, IOptions<JwtOptions> opcoes)
        {
            _db = db;
            _hasher = hasher;
            _jwt = jwt;
            _opcoes = opcoes.Value;
        }

        public async Task<LoginResponse?> LoginAsync(DTOs.Request.LoginRequest request, CancellationToken ct = default)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

            // Mesma resposta para e-mail inexistente, senha errada e usuário inativo (não revela qual foi)
            if (usuario is null || !usuario.Ativo || !_hasher.Verify(request.Senha, usuario.SenhaHash))
                return null;

            var (token, _) = _jwt.GerarAccessToken(usuario);
            return new LoginResponse(token, "Bearer", _opcoes.ExpiresMinutes * 60, UsuarioResponse.De(usuario));
        }

        public async Task<UsuarioResponse?> ObterUsuarioAsync(long id, CancellationToken ct = default)
        {
            var usuario = await _db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.Ativo, ct);
            return usuario is null ? null : UsuarioResponse.De(usuario);
        }
    }
}
