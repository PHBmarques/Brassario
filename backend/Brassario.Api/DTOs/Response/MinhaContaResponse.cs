using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>
    /// A conta de quem está logado (GET /auth/me). Traz o e-mail de recuperação, que nenhum outro
    /// endpoint expõe: só o próprio usuário vê o seu.
    /// </summary>
    public record MinhaContaResponse(long Id, string Nome, string Email, PerfilUsuario Perfil, bool Ativo,
        bool DeveTrocarSenha, bool EmailRecuperacaoConfirmado, string? EmailRecuperacao, DateTime? CriadoEm = null)
    {
        public static MinhaContaResponse De(Usuario u) =>
            new(u.Id, u.Nome, u.Email, u.Perfil, u.Ativo, u.DeveTrocarSenha,
                u.EmailRecuperacaoConfirmadoEm is not null, u.EmailRecuperacao, u.CriadoEm);
    }
}