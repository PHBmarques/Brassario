using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record UsuarioResponse(long Id, string Nome, string Email, PerfilUsuario Perfil, bool Ativo)
    {
        public static UsuarioResponse De(Usuario u) => new(u.Id, u.Nome, u.Email, u.Perfil, u.Ativo);
    }
}
