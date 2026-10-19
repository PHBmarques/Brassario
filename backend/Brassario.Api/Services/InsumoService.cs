using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class InsumoService:IInsumoService
    {
        private const int DiasCriticaPadrao = 15;
        private const int DiasAtencaoPadrao = 45;

        private readonly IInsumoRepository _repo;

        public InsumoService(IInsumoRepository repo) => _repo = repo;

        public async Task<List<InsumoResponse>> ListarAsync(
            string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default)
        {
            CategoriaInsumo? filtro = null;
            if (!string.IsNullOrWhiteSpace(categoria))
            {
                if (!Enum.TryParse<CategoriaInsumo>(categoria.Trim(), true, out var parseada) || !Enum.IsDefined(parseada))
                    throw new ValidacaoException("categoria", MensagemOpcoes<CategoriaInsumo>("Categoria inválida"));
                filtro = parseada;
            }

            var insumos = await _repo.ListarAsync(filtro, busca, incluirInativos, ct);
            return insumos.Select(InsumoResponse.De).ToList();
        }

        public async Task<InsumoResponse> ObterAsync(long id, CancellationToken ct = default) =>
            InsumoResponse.De(await BuscarOuFalharAsync(id, ct));

        public async Task<InsumoResponse> CriarAsync(InsumoRequest request, CancellationToken ct = default)
        {
            var dados = Validar(request);
            await GarantirNomeUnicoAsync(dados.Nome, null, ct);

            var insumo = new Insumo { Ativo = true };
            Aplicar(insumo, dados);

            await _repo.AdicionarAsync(insumo, ct);
            await _repo.SalvarAsync(ct);
            return InsumoResponse.De(insumo);
        }

        public async Task<InsumoResponse> AtualizarAsync(long id, InsumoRequest request, CancellationToken ct = default)
        {
            var insumo = await BuscarOuFalharAsync(id, ct);
            var dados = Validar(request);
            await GarantirNomeUnicoAsync(dados.Nome, id, ct);

            Aplicar(insumo, dados);
            await _repo.SalvarAsync(ct);
            return InsumoResponse.De(insumo);
        }

        // RN-16: não existe exclusão; o insumo é apenas inativado
        public async Task<InsumoResponse> AlterarStatusAsync(long id, bool ativo, CancellationToken ct = default)
        {
            var insumo = await BuscarOuFalharAsync(id, ct);
            insumo.Ativo = ativo;
            await _repo.SalvarAsync(ct);
            return InsumoResponse.De(insumo);
        }

        // ---------- Apoio ----------

        private async Task<Insumo> BuscarOuFalharAsync(long id, CancellationToken ct) =>
            await _repo.ObterPorIdAsync(id, ct) ?? throw new RecursoNaoEncontradoException("Insumo", id);

        private async Task GarantirNomeUnicoAsync(string nome, long? ignorarId, CancellationToken ct)
        {
            if (await _repo.ExisteNomeAsync(nome, ignorarId, ct))
                throw new ConflitoException($"Já existe um insumo com o nome \"{nome}\".");
        }

        private static void Aplicar(Insumo insumo, DadosValidos d)
        {
            insumo.Nome = d.Nome;
            insumo.Categoria = d.Categoria;
            insumo.UnidadeMedida = d.Unidade;
            insumo.EstoqueMinimo = d.EstoqueMinimo;
            insumo.DiasValidadeCritica = d.DiasCritica;
            insumo.DiasValidadeAtencao = d.DiasAtencao;
        }

        private record DadosValidos(
            string Nome, CategoriaInsumo Categoria, UnidadeMedida Unidade,
            decimal EstoqueMinimo, int DiasCritica, int DiasAtencao);

        // Valida tudo de uma vez e devolve todos os campos com problema (400)
        private static DadosValidos Validar(InsumoRequest r)
        {
            var erros = new Dictionary<string, string[]>();

            var nome = (r.Nome ?? string.Empty).Trim();
            if (nome.Length == 0) erros["nome"] = new[] { "O nome é obrigatório." };
            else if (nome.Length > 120) erros["nome"] = new[] { "O nome deve ter no máximo 120 caracteres." };

            if (!Enum.TryParse<CategoriaInsumo>((r.Categoria ?? string.Empty).Trim(), true, out var categoria)
                || !Enum.IsDefined(categoria))
                erros["categoria"] = new[] { MensagemOpcoes<CategoriaInsumo>("Categoria inválida") };

            if (!Enum.TryParse<UnidadeMedida>((r.UnidadeMedida ?? string.Empty).Trim(), true, out var unidade)
                || !Enum.IsDefined(unidade))
                erros["unidadeMedida"] = new[] { MensagemOpcoes<UnidadeMedida>("Unidade de medida inválida") };

            if (r.EstoqueMinimo < 0)
                erros["estoqueMinimo"] = new[] { "O estoque mínimo não pode ser negativo." };

            var critica = r.DiasValidadeCritica ?? DiasCriticaPadrao;
            var atencao = r.DiasValidadeAtencao ?? DiasAtencaoPadrao;
            if (critica < 0)
                erros["diasValidadeCritica"] = new[] { "Os dias de validade crítica não podem ser negativos." };
            if (atencao < critica)
                erros["diasValidadeAtencao"] = new[] { "Os dias de atenção devem ser maiores ou iguais aos dias de validade crítica." };

            if (erros.Count > 0) throw new ValidacaoException(erros);

            return new DadosValidos(nome, categoria, unidade, r.EstoqueMinimo, critica, atencao);
        }

        private static string MensagemOpcoes<T>(string inicio) where T : struct, Enum =>
            $"{inicio}. Use: {string.Join(", ", Enum.GetNames<T>())}.";
    }
}
