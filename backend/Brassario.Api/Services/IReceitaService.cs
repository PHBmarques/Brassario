using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;

namespace Brassario.Api.Services
{
    public interface IReceitaService
    {
        Task<List<ReceitaResumoResponse>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default);
        Task<ReceitaResponse> ObterAsync(long id, CancellationToken ct = default);
        Task<ReceitaResponse> CriarAsync(ReceitaRequest request, CancellationToken ct = default);
        Task<ReceitaResponse> AtualizarAsync(long id, ReceitaRequest request, CancellationToken ct = default);
        Task<ReceitaResponse> AlterarStatusAsync(long id, bool ativa, CancellationToken ct = default);

        /// <summary>
        /// Para a simulação (API-7) e para Produzir Batelada (API-8): devolve a receita com os itens
        /// e falha com 404 se não existir ou com 422 (RECEITA_INATIVA) se estiver inativa.
        /// </summary>
        Task<Receita> ObterParaProducaoAsync(long id, CancellationToken ct = default);
    }
}
