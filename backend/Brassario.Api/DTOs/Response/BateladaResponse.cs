using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>Um lote que foi consumido pela batelada.</summary>
    public record BateladaConsumoResponse(
        long LoteId,
        string CodigoLote,
        DateOnly Validade,
        long InsumoId,
        string Insumo,
        UnidadeMedida UnidadeMedida,
        decimal Quantidade,
        decimal Custo);

    /// <summary>Batelada produzida, com os lotes consumidos na baixa FEFO.</summary>
    public record BateladaResponse(
        long Id,
        string Codigo,
        long ReceitaId,
        string Receita,
        decimal VolumePlanejado,
        StatusBatelada Status,
        decimal CustoTotal,
        DateTime DataProducao,
        string? Observacoes,
        List<BateladaConsumoResponse> Consumos);
}
