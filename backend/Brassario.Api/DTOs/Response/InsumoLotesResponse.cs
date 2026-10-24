using Brassario.Api.Models;
using Brassario.Api.Services;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>Um lote na tela do insumo.</summary>
    /// <param name="OrdemDeUso">Posição na fila de uso (1 = o primeiro a ser consumido). Nula para lote vencido ou esgotado.</param>
    /// <param name="SituacaoValidade">VERDE, AMARELO ou VERMELHO, pelos limites do insumo.</param>
    public record LoteDoInsumoResponse(
        long Id,
        string CodigoLote,
        long? FornecedorId,
        string? FornecedorNome,
        string? NotaFiscal,
        DateOnly DataEntrada,
        DateOnly DataValidade,
        decimal QuantidadeInicial,
        decimal QuantidadeAtual,
        decimal PrecoUnitario,
        int DiasParaVencer,
        SituacaoValidade SituacaoValidade,
        bool Vencido,
        int? OrdemDeUso)
    {
        public static LoteDoInsumoResponse De(LoteInsumo l, Insumo insumo, DateOnly hoje, int? ordemDeUso) => new(
            l.Id, l.CodigoLote, l.FornecedorId, l.Fornecedor?.Nome, l.NotaFiscal,
            l.DataEntrada, l.DataValidade, l.QuantidadeInicial, l.QuantidadeAtual, l.PrecoUnitario,
            SituacaoValidadeCalculador.DiasParaVencer(l.DataValidade, hoje),
            SituacaoValidadeCalculador.Calcular(l.DataValidade, hoje, insumo.DiasValidadeCritica, insumo.DiasValidadeAtencao),
            l.DataValidade < hoje,
            ordemDeUso);
    }

    /// <summary>Tela do insumo: o resumo, os lotes com saldo na ordem de uso e, em lista separada, os esgotados.</summary>
    /// <param name="Lotes">Lotes com saldo: primeiro os que podem ser usados, na ordem de uso; depois, os vencidos (sem ordem de uso).</param>
    /// <param name="Esgotados">Lotes que já zeraram, do mais recente para o mais antigo.</param>
    public record InsumoLotesResponse(
        InsumoResumoResponse Resumo,
        int DiasValidadeCritica,
        int DiasValidadeAtencao,
        List<LoteDoInsumoResponse> Lotes,
        List<LoteDoInsumoResponse> Esgotados);
}
