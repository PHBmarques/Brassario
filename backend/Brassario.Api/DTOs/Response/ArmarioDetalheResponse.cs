using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>Um lote dentro do detalhe do Armário.</summary>
    public record ArmarioLoteResponse(
    long Id,
    string CodigoLote,
    long? FornecedorId,
    string? FornecedorNome,
    DateOnly DataEntrada,
    DateOnly DataValidade,
    decimal QuantidadeInicial,
    decimal QuantidadeAtual,
    decimal PrecoUnitario,
    int DiasParaVencer,
    SituacaoValidade SituacaoValidade,
    bool Vencido,
    /// <summary>Posição na fila FEFO (1 = o primeiro a ser consumido). Nula para lote vencido ou esgotado.</summary>
    int? OrdemFefo);

    /// <summary>Detalhe de um insumo no Armário: o resumo e os lotes em ordem FEFO.</summary>
    public record ArmarioDetalheResponse(
        ArmarioItemResponse Resumo,
        int DiasValidadeCritica,
        int DiasValidadeAtencao,
        List<ArmarioLoteResponse> Lotes);
}
