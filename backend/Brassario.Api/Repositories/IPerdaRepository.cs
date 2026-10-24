using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface IPerdaRepository
    {
        /// <summary>Executa a operação em UMA transação: se algo lançar exceção, tudo é desfeito.</summary>
        Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default);

        /// <summary>
        /// Trava o lote (SELECT ... FOR UPDATE), rastreado, para duas perdas ou uma produção ao mesmo tempo
        /// não usarem o mesmo saldo. Só dentro de ExecutarEmTransacaoAsync. Nulo se o lote não existe.
        /// </summary>
        Task<LoteInsumo?> BloquearLoteAsync(long loteId, CancellationToken ct = default);

        Task<Insumo?> ObterInsumoAsync(long insumoId, CancellationToken ct = default);

        Task AdicionarMovimentacaoAsync(MovimentacaoEstoque movimentacao, CancellationToken ct = default);

        Task SalvarAsync(CancellationToken ct = default);
    }
}
