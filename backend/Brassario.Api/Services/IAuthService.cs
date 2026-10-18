using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct = default);

        Task<UsuarioResponse?> ObterUsuarioAsync(long id, CancellationToken ct = default);
    }
}
