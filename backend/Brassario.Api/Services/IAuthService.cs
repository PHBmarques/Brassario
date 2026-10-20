using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public record SessaoEmitida(LoginResponse Resposta, string RefreshToken, DateTime RefreshExpiraEm);

    public interface IAuthService
    {
        /// <summary>Confere e-mail e senha e abre a sessão (access token + refresh token). Null se a credencial é inválida ou o usuário está inativo.</summary>
        Task<SessaoEmitida?> IniciarSessaoAsync(LoginRequest request, CancellationToken ct = default);

        /// <summary>
        /// Troca um refresh token válido por um novo par de tokens (rotação: o antigo é revogado).
        /// Null se o token não existe, expirou, foi revogado ou o usuário está inativo.
        /// </summary>
        Task<SessaoEmitida?> RenovarSessaoAsync(string? refreshToken, CancellationToken ct = default);

        /// <summary>Revoga o refresh token do usuário (logout). Não falha se o token não existir.</summary>
        Task EncerrarSessaoAsync(long usuarioId, string? refreshToken, CancellationToken ct = default);

        Task<UsuarioResponse?> ObterUsuarioAsync(long id, CancellationToken ct = default);
    }
}
