using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response;

/// <summary>Um ingrediente da receita, já com o nome e a unidade do insumo.</summary>
public record ReceitaItemResponse(
    long InsumoId,
    string InsumoNome,
    UnidadeMedida UnidadeMedida,
    decimal Quantidade,
    bool InsumoAtivo)
{
    public static ReceitaItemResponse De(ReceitaItem i) => new(
        i.InsumoId, i.Insumo.Nome, i.Insumo.UnidadeMedida, i.Quantidade, i.Insumo.Ativo);
}

/// <summary>Ficha completa da receita, com todos os ingredientes.</summary>
public record ReceitaResponse(
    long Id,
    string Nome,
    string? Estilo,
    decimal VolumeBaseLitros,
    string? ModoPreparo,
    bool Ativa,
    List<ReceitaItemResponse> Itens)
{
    public static ReceitaResponse De(Receita r) => new(
        r.Id, r.Nome, r.Estilo, r.VolumeBaseLitros, r.ModoPreparo, r.Ativa,
        r.Itens.OrderBy(i => i.Insumo.Nome).Select(ReceitaItemResponse.De).ToList());
}

/// <summary>Linha da lista de receitas (sem os ingredientes).</summary>
public record ReceitaResumoResponse(
    long Id,
    string Nome,
    string? Estilo,
    decimal VolumeBaseLitros,
    bool Ativa,
    int QuantidadeItens)
{
    public static ReceitaResumoResponse De(Receita r) => new(
        r.Id, r.Nome, r.Estilo, r.VolumeBaseLitros, r.Ativa, r.Itens.Count);
}