using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface ILoteRepository
    {
        /// <summary>Já existe um lote com esse código para o insumo? (sem diferenciar maiúsculas)</summary>
        Task<bool> ExisteCodigoAsync(long insumoId, string codigoLote, CancellationToken ct = default);

        /// <summary>
        /// Lotes dos insumos informados, com o fornecedor carregado (somente leitura).
        /// Por padrão traz só os lotes que ainda têm saldo; com incluirEsgotados traz também os que zeraram.
        /// </summary>
        Task<List<LoteInsumo>> ListarPorInsumosAsync(IReadOnlyCollection<long> insumoIds, bool incluirEsgotados, CancellationToken ct = default);

        Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default);

        /// <summary>Grava tudo o que foi adicionado ou alterado em uma única transação.</summary>
        Task SalvarAsync(CancellationToken ct = default);
    }
}
