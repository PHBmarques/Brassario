using System.Security.Claims;

namespace Brassario.Api.Security
{
    public static class ClaimsPrincipalExtensions
    {
        // <summary>Id do usuário logado (claim "sub" do token), ou null se o token não tiver um id válido.</summary>
        public static long? ObterUsuarioId(this ClaimsPrincipal usuario) =>
            long.TryParse(usuario.FindFirst("sub")?.Value, out var id) ? id : null;
    }
}
