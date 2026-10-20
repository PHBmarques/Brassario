using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record LotePrevistoResponse(
    long LoteId,
    string CodigoLote,
    DateOnly Validade,
    decimal Quantidade);

    /// <summary>Situação de um ingrediente para o volume pedido.</summary>
    public record DisponibilidadeItemResponse(
    long InsumoId,
    string Insumo,
    UnidadeMedida UnidadeMedida,
    decimal Necessario,
    decimal Disponivel,
    bool Suficiente,
    decimal Falta,
    List<LotePrevistoResponse> LotesPrevistos);

    /// <summary>Resultado da simulação de produção. Nada é alterado no estoque.</summary>
    public record DisponibilidadeResponse(
    long ReceitaId,
    string Receita,
    decimal Volume,
    bool PodeProduzir,
    List<DisponibilidadeItemResponse> Itens);
}
