using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface IInsumoRepository
    {
        Task<List<Insumo>> ListarAsync(CategoriaInsumo? categoria, string? busca, bool incluirInativos, CancellationToken ct = default);
        Task<Insumo?> ObterPorIdAsync(long id, CancellationToken ct = default);

        /// <summary>Confere se já existe um insumo com esse nome (sem diferenciar maiúsculas), ignorando o próprio id na edição.</summary>
        Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default);

        Task AdicionarAsync(Insumo insumo, CancellationToken ct = default);
        Task SalvarAsync(CancellationToken ct = default);
    }
}
