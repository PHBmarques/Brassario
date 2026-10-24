using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class PreviaProducaoService : IPreviaProducaoService
    {
        private readonly IReceitaService _receitas;
        private readonly ILoteRepository _lotes;
        private readonly IRelogio _relogio;

        public PreviaProducaoService(IReceitaService receitas, ILoteRepository lotes, IRelogio relogio)
        {
            _receitas = receitas;
            _lotes = lotes;
            _relogio = relogio;
        }

        public async Task<PreviaProducaoResponse> PreverAsync(long receitaId, decimal? quantidade, CancellationToken ct = default)
        {
            var litros = ValidarQuantidade(quantidade);

            // 404 se não existe e 422 STATUS_INVALIDO se estiver inativa
            var receita = await _receitas.ObterParaProducaoAsync(receitaId, ct);

            var hoje = _relogio.Hoje;
            var itens = receita.Itens.OrderBy(i => i.Insumo.Nome).ThenBy(i => i.InsumoId).ToList();
            var lotes = await _lotes.ListarPorInsumosAsync(itens.Select(i => i.InsumoId).Distinct().ToArray(), false, ct);

            // O mesmo cálculo que a baixa usa: o que aparece aqui é o que será descontado
            var plano = PlanoDeBaixa.Planejar(itens, receita.VolumeBaseLitros, litros, lotes, hoje);

            var linhas = new List<PreviaLinhaResponse>();
            var problemas = new List<PreviaProblemaResponse>();
            var custos = new List<decimal>();

            foreach (var ing in plano.Ingredientes)
            {
                var insumo = ing.Item.Insumo;

                if (ing.Situacao == SituacaoNoPlano.Ok)
                {
                    foreach (var (lote, qtd) in ing.Retiradas)
                    {
                        linhas.Add(new PreviaLinhaResponse(
                            insumo.Id, insumo.Nome, insumo.UnidadeMedida, ing.Necessario, ing.Disponivel,
                            SituacaoIngredientePrevia.DISPONIVEL, lote.Id, lote.CodigoLote, lote.DataValidade, qtd));
                        custos.Add(CalculoProducao.Custo(qtd, lote.PrecoUnitario));
                    }
                    continue;
                }

                linhas.Add(new PreviaLinhaResponse(
                    insumo.Id, insumo.Nome, insumo.UnidadeMedida, ing.Necessario, ing.Disponivel,
                    SituacaoIngredientePrevia.FALTA_ESTOQUE, null, null, null, 0m));

                if (ing.Situacao == SituacaoNoPlano.SoVencido)
                {
                    // Só há lote vencido: o aviso mostra qual lote venceu (e quando)
                    foreach (var lote in ing.LotesVencidos)
                        problemas.Add(new PreviaProblemaResponse(
                            TipoProblemaPrevia.VENCIDO, insumo.Id, insumo.Nome, insumo.UnidadeMedida,
                            null, lote.Id, lote.CodigoLote, lote.DataValidade));
                }
                else
                {
                    problemas.Add(new PreviaProblemaResponse(
                        TipoProblemaPrevia.FALTA, insumo.Id, insumo.Nome, insumo.UnidadeMedida,
                        ing.Falta, null, null, null));
                }
            }

            // Com problema o custo cobre só os ingredientes que têm estoque; a tela não deixa confirmar mesmo assim
            return new PreviaProducaoResponse(
                receita.Id, receita.Nome, litros, !plano.TemProblema,
                CalculoProducao.CustoTotal(custos), linhas, problemas);
        }

        // O banco guarda o volume com 2 casas decimais
        private static decimal ValidarQuantidade(decimal? quantidade)
        {
            if (quantidade is null || quantidade <= 0)
                throw new ValidacaoException("quantidade", "Informe a quantidade a produzir, em litros, maior que zero.");

            var arredondado = Math.Round(quantidade.Value, 2, MidpointRounding.AwayFromZero);
            if (arredondado < 0.01m)
                throw new ValidacaoException("quantidade", "A quantidade mínima é 0,01 litro.");
            if (arredondado > 99_999_999m)
                throw new ValidacaoException("quantidade", "A quantidade é grande demais.");

            return arredondado;
        }
    }
}
