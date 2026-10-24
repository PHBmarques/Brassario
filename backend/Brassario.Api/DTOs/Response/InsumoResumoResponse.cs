using Brassario.Api.Models;
using Brassario.Api.Services;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>Cartão do armário: um por insumo, mesmo sem nenhum lote (aparece com saldo zero).</summary>
    /// <param name="SaldoTotal">Soma dos lotes com saldo, vencidos ou não: o que existe fisicamente no armário.</param>
    /// <param name="SaldoUtilizavel">Parte do saldo que ainda pode ser usada (lotes não vencidos). É o que vale para o estoque mínimo.</param>
    /// <param name="QuantidadeLotes">Número de lotes com saldo.</param>
    /// <param name="ValidadeMaisProxima">Menor validade entre os lotes com saldo. Se já passou, é a data do lote vencido.</param>
    /// <param name="DiasParaVencer">Dias até a validade mais próxima. Negativo quando já venceu.</param>
    /// <param name="Situacao">OK, ATENCAO, CRITICO, VENCIDO ou ABAIXO_DO_MINIMO (a primeira que valer, nesta ordem: VENCIDO, CRITICO, ABAIXO_DO_MINIMO, ATENCAO, OK).</param>
    /// <param name="AbaixoDoMinimo">Sempre informa se o saldo utilizável está abaixo do mínimo, mesmo quando a situação mostra outra coisa.</param>
    public record InsumoResumoResponse(
        long InsumoId,
        string Nome,
        CategoriaInsumo Categoria,
        UnidadeMedida UnidadeMedida,
        bool Ativo,
        decimal EstoqueMinimo,
        decimal SaldoTotal,
        decimal SaldoUtilizavel,
        int QuantidadeLotes,
        DateOnly? ValidadeMaisProxima,
        int? DiasParaVencer,
        SituacaoInsumo Situacao,
        bool AbaixoDoMinimo)
    {
        /// <summary>Monta o resumo a partir dos lotes do insumo. Lote sem saldo é ignorado; lista vazia dá saldo zero.</summary>
        public static InsumoResumoResponse De(Insumo insumo, IEnumerable<LoteInsumo> lotes, DateOnly hoje)
        {
            var comSaldo = lotes.Where(l => l.QuantidadeAtual > 0).ToList();
            var utilizaveis = CalculoProducao.FilaFefo(comSaldo, hoje);

            var saldoTotal = comSaldo.Sum(l => l.QuantidadeAtual);
            var saldoUtilizavel = utilizaveis.Sum(l => l.QuantidadeAtual);
            var maisProximo = CalculoProducao.OrdemDeUso(comSaldo).FirstOrDefault();

            var abaixoDoMinimo = saldoUtilizavel < insumo.EstoqueMinimo;

            // Ordem de força: VENCIDO > CRITICO > ABAIXO_DO_MINIMO > ATENCAO > OK
            var corDoPrimeiro = utilizaveis.Count == 0
                ? (SituacaoValidade?)null
                : SituacaoValidadeCalculador.Calcular(utilizaveis[0].DataValidade, hoje, insumo.DiasValidadeCritica, insumo.DiasValidadeAtencao);

            SituacaoInsumo situacao;
            if (comSaldo.Any(l => l.DataValidade < hoje)) situacao = SituacaoInsumo.VENCIDO;
            else if (corDoPrimeiro == SituacaoValidade.VERMELHO) situacao = SituacaoInsumo.CRITICO;
            else if (abaixoDoMinimo) situacao = SituacaoInsumo.ABAIXO_DO_MINIMO;
            else if (corDoPrimeiro == SituacaoValidade.AMARELO) situacao = SituacaoInsumo.ATENCAO;
            else situacao = SituacaoInsumo.OK;

            return new InsumoResumoResponse(
                insumo.Id, insumo.Nome, insumo.Categoria, insumo.UnidadeMedida, insumo.Ativo, insumo.EstoqueMinimo,
                saldoTotal, saldoUtilizavel, comSaldo.Count,
                maisProximo?.DataValidade,
                maisProximo is null ? null : SituacaoValidadeCalculador.DiasParaVencer(maisProximo.DataValidade, hoje),
                situacao, abaixoDoMinimo);
        }
    }
}
