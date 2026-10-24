using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public enum SituacaoIngredientePrevia { DISPONIVEL, FALTA_ESTOQUE }

    public enum TipoProblemaPrevia { FALTA, VENCIDO }

    /// <summary>
    /// Uma linha da tabela da tela Produzir. Ingrediente que passa por dois lotes vira duas linhas;
    /// ingrediente em falta vira uma linha só, sem lote (nada sai dele).
    /// </summary>
    public record PreviaLinhaResponse(
        long InsumoId,
        string Insumo,
        UnidadeMedida UnidadeMedida,
        decimal Necessario,
        decimal Disponivel,
        SituacaoIngredientePrevia Situacao,
        long? LoteId,
        string? Lote,
        DateOnly? Validade,
        decimal SaiDoLote);

    /// <summary>
    /// Item do aviso da tela. FALTA: insumo, quantidade que falta e unidade.
    /// VENCIDO: insumo, lote e validade (um item por lote vencido com saldo).
    /// </summary>
    public record PreviaProblemaResponse(
        TipoProblemaPrevia Tipo,
        long InsumoId,
        string Insumo,
        UnidadeMedida UnidadeMedida,
        decimal? Falta,
        long? LoteId,
        string? Lote,
        DateOnly? Validade);

    /// <summary>Prévia da produção: o que será descontado, sem gravar nada.</summary>
    public record PreviaProducaoResponse(
        long ReceitaId,
        string Receita,
        decimal Quantidade,
        bool PodeConfirmar,
        decimal CustoEstimado,
        List<PreviaLinhaResponse> Itens,
        List<PreviaProblemaResponse> Problemas);
}
