using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record UsuarioResponse(long Id, string Nome, string Email, PerfilUsuario Perfil, bool Ativo,
        bool DeveTrocarSenha = false, bool EmailRecuperacaoConfirmado = false)
    {
        public static UsuarioResponse De(Usuario u) =>
            new(u.Id, u.Nome, u.Email, u.Perfil, u.Ativo, u.DeveTrocarSenha, u.EmailRecuperacaoConfirmadoEm is not null);
    }
}