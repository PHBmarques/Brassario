using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface IReceitaRepository
    {
        /// <summary>Lista as receitas (com os itens, para contar), em ordem de nome.</summary>
        Task<List<Receita>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default);

        /// <summary>Busca a receita com os itens e os insumos de cada item.</summary>
        Task<Receita?> ObterPorIdAsync(long id, CancellationToken ct = default);

        /// <summary>Confere se já existe uma receita com esse nome (sem diferenciar maiúsculas), ignorando o próprio id na edição.</summary>
        Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default);

        /// <summary>Busca os insumos pelos ids (os que não existirem simplesmente não voltam).</summary>
        Task<List<Insumo>> ObterInsumosAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default);

        Task AdicionarAsync(Receita receita, CancellationToken ct = default);
        Task SalvarAsync(CancellationToken ct = default);
    }
}
