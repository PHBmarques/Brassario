using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface IUsuarioRepository
    {
        Task<List<Usuario>> ListarAsync(string? busca, bool incluirInativos, CancellationToken ct = default);
        Task<Usuario?> ObterPorIdAsync(long id, CancellationToken ct = default);

        /// <summary>Já existe usuário com esse e-mail? (sem diferenciar maiúsculas; ignora o id informado na edição)</summary>
        Task<bool> ExisteEmailAsync(string email, long? ignorarId, CancellationToken ct = default);

        /// <summary>Quantos ADMIN ativos existem além do usuário informado.</summary>
        Task<int> ContarAdminsAtivosExcetoAsync(long usuarioId, CancellationToken ct = default);

        /// <summary>Marca como revogados os refresh tokens do usuário (encerra as sessões). Gravado no próximo SalvarAsync.</summary>
        Task RevogarSessoesAsync(long usuarioId, CancellationToken ct = default);

        Task AdicionarAsync(Usuario usuario, CancellationToken ct = default);
        Task SalvarAsync(CancellationToken ct = default);
    }
}
