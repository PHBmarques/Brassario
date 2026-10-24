using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record PerdaResponse(
        long MovimentacaoId,
        long LoteId,
        string CodigoLote,
        long InsumoId,
        string Insumo,
        UnidadeMedida UnidadeMedida,
        /// <summary>Quantidade perdida (positiva). No histórico a movimentação PERDA é negativa.</summary>
        decimal Quantidade,
        decimal SaldoAnterior,
        decimal SaldoAtual,
        string Motivo,
        DateTime DataHora);
}
