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

        /// <summary>Gera uma senha temporária (devolvida só nesta resposta), exige a troca no próximo acesso e encerra as sessões. Não vale para si mesmo nem para inativos.</summary>
        Task<SenhaTemporariaResponse> GerarSenhaTemporariaAsync(long id, long usuarioLogadoId, CancellationToken ct = default);
    }
}
