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

        /// <summary>Só confere os campos de um fornecedor novo (400 por campo). Com prefixo, os campos saem como "novoFornecedor.nome".</summary>
        void ValidarNovo(FornecedorRequest request, string? prefixoCampos = null);

        /// <summary>
        /// Valida (400) e confere o nome único (409 NOME_DUPLICADO) e devolve o fornecedor novo SEM gravar.
        /// Quem chama grava junto com o resto, na mesma transação (entrada composta).
        /// </summary>
        Task<Brassario.Api.Models.Fornecedor> PrepararNovoAsync(FornecedorRequest request, string? prefixoCampos, CancellationToken ct = default);
        Task<FornecedorResponse> AtualizarAsync(long id, FornecedorRequest request, CancellationToken ct = default);
    }
}
