using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class FornecedorService : IFornecedorService
    {
        /// <summary>Máximo de itens da caixa de busca (a digitação não pode pesar).</summary>
        public const int LimiteBusca = 10;

        private readonly IFornecedorCadastroRepository _repo;

        public FornecedorService(IFornecedorCadastroRepository repo) => _repo = repo;

        public async Task<List<FornecedorResponse>> ListarAsync(string? busca, CancellationToken ct = default) =>
            (await _repo.ListarAsync(busca, ct)).Select(FornecedorResponse.De).ToList();

        public async Task<List<FornecedorBuscaResponse>> BuscarAsync(string? busca, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(busca)) return new List<FornecedorBuscaResponse>();

            var todos = await _repo.ListarAsync(null, ct);
            return TextoBusca.Filtrar(todos.Where(f => f.Ativo), busca, f => f.Nome)
                .Take(LimiteBusca)
                .Select(f => new FornecedorBuscaResponse(f.Id, f.Nome))
                .ToList();
        }

        public async Task<FornecedorResponse> ObterAsync(long id, CancellationToken ct = default) =>
            FornecedorResponse.De(await BuscarOuFalharAsync(id, ct));

        public async Task<FornecedorResponse> CriarAsync(FornecedorRequest request, CancellationToken ct = default)
        {
            var fornecedor = await PrepararNovoAsync(request, null, ct);
            await _repo.AdicionarAsync(fornecedor, ct);
            await _repo.SalvarAsync(ct);
            return FornecedorResponse.De(fornecedor);
        }

        public void ValidarNovo(FornecedorRequest request, string? prefixoCampos = null)
        {
            try { Validar(request); }
            catch (ValidacaoException e) when (!string.IsNullOrEmpty(prefixoCampos))
            {
                throw CamposComPrefixo.Aplicar(e, prefixoCampos);
            }
        }

        public async Task<Fornecedor> PrepararNovoAsync(FornecedorRequest request, string? prefixoCampos, CancellationToken ct = default)
        {
            ValidarNovo(request, prefixoCampos);
            var (nome, contato) = Validar(request);
            await GarantirNomeUnicoAsync(nome, null, ct, prefixoCampos);

            return new Fornecedor { Nome = nome, Contato = contato, Ativo = true };
        }

        public async Task<FornecedorResponse> AtualizarAsync(long id, FornecedorRequest request, CancellationToken ct = default)
        {
            var fornecedor = await BuscarOuFalharAsync(id, ct);
            var (nome, contato) = Validar(request);
            await GarantirNomeUnicoAsync(nome, id, ct);

            fornecedor.Nome = nome;
            fornecedor.Contato = contato;
            await _repo.SalvarAsync(ct);
            return FornecedorResponse.De(fornecedor);
        }

        // ---------- Apoio ----------

        private async Task<Fornecedor> BuscarOuFalharAsync(long id, CancellationToken ct) =>
            await _repo.ObterPorIdAsync(id, ct) ?? throw new RecursoNaoEncontradoException("Fornecedor", id);

        private async Task GarantirNomeUnicoAsync(string nome, long? ignorarId, CancellationToken ct, string? prefixoCampos = null)
        {
            if (await _repo.ExisteNomeAsync(nome, ignorarId, ct))
                throw ConflitoException.NomeDuplicado($"Já existe um fornecedor com o nome \"{nome}\".", (prefixoCampos ?? string.Empty) + "nome");
        }

        private static (string Nome, string? Contato) Validar(FornecedorRequest r)
        {
            var erros = new Dictionary<string, string[]>();

            var nome = (r.Nome ?? string.Empty).Trim();
            if (nome.Length == 0) erros["nome"] = new[] { "O nome é obrigatório." };
            else if (nome.Length > 120) erros["nome"] = new[] { "O nome deve ter no máximo 120 caracteres." };

            var contato = string.IsNullOrWhiteSpace(r.Contato) ? null : r.Contato.Trim();
            if (contato is { Length: > 160 })
                erros["contato"] = new[] { "O contato deve ter no máximo 160 caracteres." };

            if (erros.Count > 0) throw new ValidacaoException(erros);

            return (nome, contato);
        }
    }
}
