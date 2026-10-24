using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface IFornecedorService
    {
        Task<List<FornecedorResponse>> ListarAsync(string? busca, CancellationToken ct = default);

        /// <summary>
        /// Busca para a caixa de escolha da entrada: por nome, sem diferenciar maiúsculas nem acentos,
        /// só fornecedores ativos, no máximo 10 itens leves. Sem resultado devolve lista vazia.
        /// </summary>
        Task<List<FornecedorBuscaResponse>> BuscarAsync(string? busca, CancellationToken ct = default);

        Task<FornecedorResponse> ObterAsync(long id, CancellationToken ct = default);
        Task<FornecedorResponse> CriarAsync(FornecedorRequest request, CancellationToken ct = default);
        Task<FornecedorResponse> AtualizarAsync(long id, FornecedorRequest request, CancellationToken ct = default);
    }
}
