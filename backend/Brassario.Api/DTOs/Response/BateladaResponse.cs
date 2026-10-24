using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>Um lote que foi consumido pela produção.</summary>
    /// <remarks>
    /// SaldoAtualLote é o saldo do lote no momento da consulta; na resposta do POST é o saldo logo depois da produção.
    /// </remarks>
    public record BateladaConsumoResponse(
        long LoteId,
        string CodigoLote,
        DateOnly Validade,
        long InsumoId,
        string Insumo,
        UnidadeMedida UnidadeMedida,
        decimal Quantidade,
        decimal Custo,
        decimal? SaldoAtualLote = null);

    /// <summary>
    /// Resumo por insumo do que a produção consumiu (soma dos lotes). SaldoAntes e SaldoDepois são o estoque utilizável
    /// do insumo (sem lote vencido) antes e depois da produção; só vêm na resposta do POST, no detalhe (GET) ficam nulos.
    /// </summary>
    public record BateladaInsumoResponse(
        long InsumoId,
        string Insumo,
        UnidadeMedida UnidadeMedida,
        decimal Quantidade,
        decimal Custo,
        decimal? SaldoAntes = null,
        decimal? SaldoDepois = null);

    /// <summary>Produção registrada, com os lotes consumidos na baixa FEFO.</summary>
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
        List<BateladaConsumoResponse> Consumos,
        decimal? VolumeFinal = null,
        decimal? RendimentoPercentual = null,
        decimal? CustoPorLitro = null,
        string? MotivoCancelamento = null,
        List<BateladaConsumoResponse>? Estornos = null,
        List<BateladaInsumoResponse>? Insumos = null,
        DateTime? CanceladoEm = null);
}
