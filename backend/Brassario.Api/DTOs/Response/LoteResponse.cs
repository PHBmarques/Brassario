using Brassario.Api.Models;
using Brassario.Api.Services;

namespace Brassario.Api.DTOs.Response
{
    public record LoteResponse(
    long Id,
    long InsumoId,
    string InsumoNome,
    long? FornecedorId,
    string? FornecedorNome,
    string CodigoLote,
    DateOnly DataEntrada,
    DateOnly DataValidade,
    decimal QuantidadeInicial,
    decimal QuantidadeAtual,
    decimal PrecoUnitario,
    int DiasParaVencer,
    SituacaoValidade SituacaoValidade)
    {
        public static LoteResponse De(LoteInsumo l, Insumo insumo, Fornecedor? fornecedor, DateOnly hoje) => new(
        l.Id, insumo.Id, insumo.Nome, fornecedor?.Id, fornecedor?.Nome,
        l.CodigoLote, l.DataEntrada, l.DataValidade, l.QuantidadeInicial, l.QuantidadeAtual, l.PrecoUnitario,
        SituacaoValidadeCalculador.DiasParaVencer(l.DataValidade, hoje),
        SituacaoValidadeCalculador.Calcular(l.DataValidade, hoje, insumo.DiasValidadeCritica, insumo.DiasValidadeAtencao));
    }
}
