using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>
    /// Uma produção na tabela de Produções (uma linha por produção).
    /// Os lotes consumidos não vêm aqui: estão em GET /bateladas/{id}/consumos.
    /// </summary>
    public record BateladaResumoResponse(
        long Id,
        string Codigo,
        long ReceitaId,
        string Receita,
        DateTime DataProducao,
        /// <summary>Litros produzidos: o volume final quando já informado; senão, o volume planejado.</summary>
        decimal Quantidade,
        decimal CustoTotal,
        long ResponsavelId,
        string Responsavel,
        StatusBatelada Status);

    /// <summary>Uma entrada ou saída de estoque de um lote (quantidade negativa é saída).</summary>
    public record MovimentacaoResponse(
        long Id,
        DateTime DataHora,
        TipoMovimentacao Tipo,
        decimal Quantidade,
        string? Motivo,
        long InsumoId,
        string Insumo,
        UnidadeMedida UnidadeMedida,
        long LoteId,
        string CodigoLote,
        long? ProducaoId,
        string? CodigoProducao,
        long ResponsavelId,
        string Responsavel);
}
