using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface IBateladaRepository
    {
        /// <summary>
        /// Executa a operação dentro de UMA transação: se qualquer coisa lançar exceção, tudo é desfeito (rollback).
        /// </summary>
        Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default);

        /// <summary>
        /// Trava (SELECT ... FOR UPDATE) os lotes com saldo dos insumos informados, já na ordem
        /// insumo e depois FEFO. Duas produções ao mesmo tempo esperam uma pela outra, sem deadlock
        /// e sem consumir o mesmo saldo duas vezes. Só pode ser chamado dentro de ExecutarEmTransacaoAsync.
        /// </summary>
        Task<List<LoteInsumo>> BloquearLotesAsync(IReadOnlyCollection<long> insumoIds, CancellationToken ct = default);

        /// <summary>Trava uma batelada e carrega seus consumos para alterar o estado sem corrida.</summary>
        Task<Batelada?> BloquearBateladaAsync(long id, CancellationToken ct = default);

        /// <summary>Trava lotes por id, inclusive os que estão com saldo zero. Só dentro da transação.</summary>
        Task<List<LoteInsumo>> BloquearLotesPorIdAsync(IReadOnlyCollection<long> loteIds, CancellationToken ct = default);

        /// <summary>Gera o próximo código do ano (ex.: B-2026-0001). Só dentro da transação.</summary>
        Task<string> ProximoCodigoAsync(int ano, CancellationToken ct = default);

        Task AdicionarAsync(Batelada batelada, CancellationToken ct = default);

        Task SalvarAsync(CancellationToken ct = default);
    }
}
