using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record DashboardResumoResponse(
        DateTime GeradoEmUtc,
        DateTime InicioPeriodoUtc,
        int InsumosAtivos,
        decimal SaldoUtilizavelTotal,
        decimal ConsumoTotalUltimos30Dias,
        int InsumosSemEstoque,
        int InsumosAbaixoDoMinimo,
        int InsumosParaCompra);

    public record DashboardConsumoResponse(
        long InsumoId,
        string InsumoNome,
        CategoriaInsumo Categoria,
        UnidadeMedida UnidadeMedida,
        decimal ConsumoUltimos30Dias,
        decimal ConsumoMedioDiario,
        decimal SaldoUtilizavel);

    public record DashboardPrevisaoResponse(
        long InsumoId,
        string InsumoNome,
        CategoriaInsumo Categoria,
        UnidadeMedida UnidadeMedida,
        decimal SaldoUtilizavel,
        decimal ConsumoMedioDiario,
        decimal? DiasRestantes,
        bool SugerirCompra);

    public record SugestaoCompraResponse(
        long InsumoId,
        string InsumoNome,
        CategoriaInsumo Categoria,
        UnidadeMedida UnidadeMedida,
        decimal SaldoUtilizavel,
        decimal EstoqueMinimo,
        decimal ConsumoMedioDiario,
        decimal? DiasRestantes,
        string Motivo);
}
