using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record InsumoResponse (long Id,
    string Nome,
    CategoriaInsumo Categoria,
    UnidadeMedida UnidadeMedida,
    decimal EstoqueMinimo,
    int DiasValidadeCritica,
    int DiasValidadeAtencao,
    bool Ativo)
    {
        public static InsumoResponse De(Insumo i) => new(
        i.Id, i.Nome, i.Categoria, i.UnidadeMedida, i.EstoqueMinimo,
        i.DiasValidadeCritica, i.DiasValidadeAtencao, i.Ativo);
    }
}
