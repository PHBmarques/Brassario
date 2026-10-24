using Brassario.Api.DTOs.Request;
using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    public interface IMovimentacaoRepository
    {
        Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default);
        Task<LoteInsumo?> BloquearLoteAsync(long id, CancellationToken ct = default);
        Task AdicionarAsync(MovimentacaoEstoque movimentacao, CancellationToken ct = default);
        Task SalvarAsync(CancellationToken ct = default);
        Task<List<MovimentacaoEstoque>> ListarAsync(MovimentacaoFiltro filtro, CancellationToken ct = default);
    }
}
