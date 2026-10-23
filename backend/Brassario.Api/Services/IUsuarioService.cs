using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IUsuarioService
    {
        Task<List<UsuarioResponse>> ListarAsync(string? busca, bool incluirInativos, CancellationToken ct = default);
        Task<UsuarioResponse> ObterAsync(long id, CancellationToken ct = default);
        Task<UsuarioResponse> CriarAsync(UsuarioRequest request, CancellationToken ct = default);

        /// <summary>Edita nome, e-mail e perfil; troca a senha (e encerra as sessões) se ela for informada. Não rebaixa o último ADMIN ativo.</summary>
        Task<UsuarioResponse> AtualizarAsync(long id, UsuarioAtualizarRequest request, CancellationToken ct = default);

        /// <summary>Ativa ou inativa. Inativar encerra as sessões do usuário. Ninguém inativa a si mesmo nem o último ADMIN ativo.</summary>
        Task<UsuarioResponse> AlterarStatusAsync(long id, bool ativo, long usuarioLogadoId, CancellationToken ct = default);

        /// <summary>
        /// O administrador redefine a senha de outro usuário: gera uma senha temporária, marca a troca pendente e
        /// encerra as sessões dele. A senha só existe na resposta. Ninguém redefine a própria senha por aqui.
        /// </summary>
        Task<SenhaTemporariaResponse> RedefinirSenhaAsync(long id, long usuarioLogadoId, CancellationToken ct = default);
    }
}
