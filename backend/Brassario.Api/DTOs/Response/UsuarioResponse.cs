using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record UsuarioResponse(long Id, string Nome, string Email, PerfilUsuario Perfil, bool Ativo,
        bool DeveTrocarSenha = false, bool EmailRecuperacaoConfirmado = false,
        DateTime? CriadoEm = null)
    {
        // O e-mail de recuperação NÃO entra aqui (só em MinhaContaResponse): o admin vê esta resposta na listagem
        public static UsuarioResponse De(Usuario u) =>
            new(u.Id, u.Nome, u.Email, u.Perfil, u.Ativo, u.DeveTrocarSenha, u.EmailRecuperacaoConfirmadoEm is not null,
                u.CriadoEm);
    }
}