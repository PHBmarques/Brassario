using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class ReceitaService : IReceitaService
    {
        private const int MaximoItens = 100;

        private readonly IReceitaRepository _repo;

        public ReceitaService(IReceitaRepository repo) => _repo = repo;

        public async Task<List<ReceitaResumoResponse>> ListarAsync(
            string? busca, bool incluirInativas, CancellationToken ct = default)
        {
            var receitas = await _repo.ListarAsync(busca, incluirInativas, ct);
            return receitas.Select(ReceitaResumoResponse.De).ToList();
        }

        public async Task<ReceitaResponse> ObterAsync(long id, CancellationToken ct = default) =>
            ReceitaResponse.De(await BuscarOuFalharAsync(id, ct));

        public async Task<ReceitaResponse> CriarAsync(ReceitaRequest request, CancellationToken ct = default)
        {
            // 1) Campos (400)
            var dados = Validar(request);

            // 2) Os insumos precisam existir (400, com o número do ingrediente)
            var insumos = await CarregarInsumosAsync(dados.Itens, ct);

            // 3) Nome repetido (409)
            await GarantirNomeUnicoAsync(dados.Nome, null, ct);

            // 4) Insumo inativo não entra em receita nova (422)
            GarantirInsumosAtivos(dados.Itens, insumos, jaNaReceita: new HashSet<long>());

            var receita = new Receita { Ativa = true };
            AplicarCampos(receita, dados);
            foreach (var item in dados.Itens)
            {
                receita.Itens.Add(new ReceitaItem
                {
                    InsumoId = item.InsumoId,
                    Insumo = insumos[item.InsumoId],
                    Quantidade = item.Quantidade
                });
            }

            await _repo.AdicionarAsync(receita, ct);
            await _repo.SalvarAsync(ct);
            return ReceitaResponse.De(receita);
        }

        public async Task<ReceitaResponse> AtualizarAsync(long id, ReceitaRequest request, CancellationToken ct = default)
        {
            var receita = await BuscarOuFalharAsync(id, ct);

            var dados = Validar(request);
            var insumos = await CarregarInsumosAsync(dados.Itens, ct);
            await GarantirNomeUnicoAsync(dados.Nome, id, ct);

            // Quem já estava na receita pode continuar, mesmo que o insumo tenha sido inativado depois.
            // Só bloqueia o ingrediente NOVO que seja de insumo inativo.
            var jaNaReceita = receita.Itens.Select(i => i.InsumoId).ToHashSet();
            GarantirInsumosAtivos(dados.Itens, insumos, jaNaReceita);

            AplicarCampos(receita, dados);
            SincronizarItens(receita, dados.Itens, insumos);

            await _repo.SalvarAsync(ct);
            return ReceitaResponse.De(receita);
        }

        // Não existe exclusão: a receita é apenas inativada (a inativa não pode ser produzida)
        public async Task<ReceitaResponse> AlterarStatusAsync(long id, bool ativa, CancellationToken ct = default)
        {
            var receita = await BuscarOuFalharAsync(id, ct);
            receita.Ativa = ativa;
            await _repo.SalvarAsync(ct);
            return ReceitaResponse.De(receita);
        }

        public async Task<Receita> ObterParaProducaoAsync(long id, CancellationToken ct = default)
        {
            var receita = await BuscarOuFalharAsync(id, ct);

            if (!receita.Ativa)
                throw new NegocioException(
                    $"A receita \"{receita.Nome}\" está inativa e não pode ser produzida.", "STATUS_INVALIDO");

            return receita;
        }

        // ---------- Apoio ----------

        private async Task<Receita> BuscarOuFalharAsync(long id, CancellationToken ct) =>
            await _repo.ObterPorIdAsync(id, ct) ?? throw new RecursoNaoEncontradoException("Receita", id);

        private async Task GarantirNomeUnicoAsync(string nome, long? ignorarId, CancellationToken ct)
        {
            if (await _repo.ExisteNomeAsync(nome, ignorarId, ct))
                throw new ConflitoException($"Já existe uma receita com o nome \"{nome}\".");
        }

        private async Task<Dictionary<long, Insumo>> CarregarInsumosAsync(List<ItemValido> itens, CancellationToken ct)
        {
            var ids = itens.Select(i => i.InsumoId).Distinct().ToList();
            var encontrados = (await _repo.ObterInsumosAsync(ids, ct)).ToDictionary(i => i.Id);

            var erros = new Dictionary<string, string[]>();
            for (var indice = 0; indice < itens.Count; indice++)
            {
                if (!encontrados.ContainsKey(itens[indice].InsumoId))
                    erros[$"itens[{indice}].insumoId"] = new[] { $"Insumo não encontrado (id {itens[indice].InsumoId})." };
            }

            if (erros.Count > 0) throw new ValidacaoException(erros);
            return encontrados;
        }

        private static void GarantirInsumosAtivos(
            List<ItemValido> itens, Dictionary<long, Insumo> insumos, HashSet<long> jaNaReceita)
        {
            var erros = new Dictionary<string, string[]>();
            var nomes = new List<string>();

            for (var indice = 0; indice < itens.Count; indice++)
            {
                var insumo = insumos[itens[indice].InsumoId];
                if (!insumo.Ativo && !jaNaReceita.Contains(insumo.Id))
                {
                    nomes.Add(insumo.Nome);
                    erros[$"itens[{indice}].insumoId"] = new[] { $"O insumo \"{insumo.Nome}\" está inativo." };
                }
            }

            if (nomes.Count > 0)
                throw new NegocioException(
                    $"A receita usa insumo inativo: {string.Join(", ", nomes)}.", "INSUMO_INATIVO", erros);
        }

        private static void AplicarCampos(Receita receita, DadosValidos d)
        {
            receita.Nome = d.Nome;
            receita.Estilo = d.Estilo;
            receita.VolumeBaseLitros = d.Volume;
            receita.ModoPreparo = d.ModoPreparo;
        }

        // Atualiza a quantidade de quem continua, remove quem saiu e adiciona quem entrou
        private static void SincronizarItens(Receita receita, List<ItemValido> novos, Dictionary<long, Insumo> insumos)
        {
            var pendentes = novos.ToDictionary(i => i.InsumoId);

            foreach (var existente in receita.Itens.ToList())
            {
                if (pendentes.TryGetValue(existente.InsumoId, out var novo))
                {
                    existente.Quantidade = novo.Quantidade;
                    pendentes.Remove(existente.InsumoId);
                }
                else
                {
                    receita.Itens.Remove(existente);
                }
            }

            foreach (var item in novos.Where(i => pendentes.ContainsKey(i.InsumoId)))
            {
                receita.Itens.Add(new ReceitaItem
                {
                    InsumoId = item.InsumoId,
                    Insumo = insumos[item.InsumoId],
                    Quantidade = item.Quantidade
                });
            }
        }

        private record ItemValido(long InsumoId, decimal Quantidade);

        private record DadosValidos(
            string Nome, string? Estilo, decimal Volume, string? ModoPreparo, List<ItemValido> Itens);

        private static void Adicionar(Dictionary<string, string[]> erros, string campo, string mensagem)
        {
            erros[campo] = erros.TryGetValue(campo, out var atuais) ? atuais.Append(mensagem).ToArray() : new[] { mensagem };
        }

        // Valida tudo de uma vez e devolve todos os campos com problema (400)
        private static DadosValidos Validar(ReceitaRequest r)
        {
            var erros = new Dictionary<string, string[]>();

            var nome = (r.Nome ?? string.Empty).Trim();
            if (nome.Length == 0) Adicionar(erros, "nome", "O nome é obrigatório.");
            else if (nome.Length > 120) Adicionar(erros, "nome", "O nome deve ter no máximo 120 caracteres.");

            var estilo = string.IsNullOrWhiteSpace(r.Estilo) ? null : r.Estilo.Trim();
            if (estilo is { Length: > 80 }) Adicionar(erros, "estilo", "O estilo deve ter no máximo 80 caracteres.");

            var modo = string.IsNullOrWhiteSpace(r.ModoPreparo) ? null : r.ModoPreparo.Trim();
            if (modo is { Length: > 5000 }) Adicionar(erros, "modoPreparo", "O modo de preparo deve ter no máximo 5000 caracteres.");

            // Volume base > 0 (o banco guarda 2 casas decimais)
            var volume = Math.Round(r.VolumeBaseLitros, 2, MidpointRounding.AwayFromZero);
            if (r.VolumeBaseLitros <= 0)
                Adicionar(erros, "volumeBaseLitros", "O volume base deve ser maior que zero.");
            else if (volume < 0.01m)
                Adicionar(erros, "volumeBaseLitros", "O volume base mínimo é 0,01 litro.");
            else if (volume > 99_999_999m)
                Adicionar(erros, "volumeBaseLitros", "O volume base é grande demais.");

            // Itens: pelo menos 1, sem insumo repetido, quantidade > 0
            var itensRequest = r.Itens ?? new List<ReceitaItemRequest>();
            var itens = new List<ItemValido>();

            if (itensRequest.Count == 0)
                Adicionar(erros, "itens", "A receita precisa ter pelo menos 1 ingrediente.");
            else if (itensRequest.Count > MaximoItens)
                Adicionar(erros, "itens", $"A receita pode ter no máximo {MaximoItens} ingredientes.");
            else
            {
                var vistos = new HashSet<long>();
                for (var i = 0; i < itensRequest.Count; i++)
                {
                    var item = itensRequest[i];
                    if (item is null)
                    {
                        Adicionar(erros, $"itens[{i}]", "Ingrediente inválido.");
                        continue;
                    }

                    if (item.InsumoId <= 0)
                        Adicionar(erros, $"itens[{i}].insumoId", "Informe o insumo do ingrediente.");
                    else if (!vistos.Add(item.InsumoId))
                        Adicionar(erros, $"itens[{i}].insumoId", "Este insumo já aparece em outro ingrediente da receita.");

                    // O banco guarda 3 casas decimais
                    var quantidade = Math.Round(item.Quantidade, 3, MidpointRounding.AwayFromZero);
                    if (item.Quantidade <= 0)
                        Adicionar(erros, $"itens[{i}].quantidade", "A quantidade deve ser maior que zero.");
                    else if (quantidade < 0.001m)
                        Adicionar(erros, $"itens[{i}].quantidade", "A quantidade mínima é 0,001.");
                    else if (quantidade > 999_999_999m)
                        Adicionar(erros, $"itens[{i}].quantidade", "A quantidade é grande demais.");

                    itens.Add(new ItemValido(item.InsumoId, quantidade));
                }
            }

            if (erros.Count > 0) throw new ValidacaoException(erros);

            return new DadosValidos(nome, estilo, volume, modo, itens);
        }
    }
}
