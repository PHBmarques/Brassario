using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface ILoteRepository
    {
        /// <summary>Já existe um lote com esse código para o insumo? (sem diferenciar maiúsculas)</summary>
        Task<bool> ExisteCodigoAsync(long insumoId, string codigoLote, CancellationToken ct = default);

        Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default);

        /// <summary>Grava tudo o que foi adicionado ou alterado em uma única transação.</summary>
        Task SalvarAsync(CancellationToken ct = default);
    }
}
