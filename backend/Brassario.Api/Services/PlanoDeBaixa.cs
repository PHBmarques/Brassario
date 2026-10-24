using Brassario.Api.Models;

namespace Brassario.Api.Services
{
    /// <summary>Situação de um ingrediente no plano de baixa.</summary>
    public enum SituacaoNoPlano
    {
        /// <summary>O saldo utilizável cobre o necessário.</summary>
        Ok,
        /// <summary>O saldo utilizável (lotes não vencidos) não cobre o necessário.</summary>
        Falta,
        /// <summary>Não há nenhum lote utilizável e os únicos lotes com saldo estão vencidos.</summary>
        SoVencido
    }

    /// <summary>O que a produção faria com um ingrediente da receita.</summary>
    public sealed record IngredienteDoPlano(
        ReceitaItem Item,
        decimal Necessario,
        decimal Disponivel,
        SituacaoNoPlano Situacao,
        IReadOnlyList<(LoteInsumo Lote, decimal Quantidade)> Retiradas,
        IReadOnlyList<LoteInsumo> LotesVencidos)
    {
        public decimal Falta => Situacao == SituacaoNoPlano.Ok ? 0m : Necessario - Disponivel;
    }

    /// <summary>
    /// ÚNICO lugar que decide o que sai de cada lote numa produção. A baixa (BateladaService) e a prévia
    /// (PreviaProducaoService) chamam esta mesma função, por isso o que a tela mostra é exatamente o que será descontado.
    /// Não altera nada: só calcula.
    /// </summary>
    public sealed class PlanoDeBaixa
    {
        public IReadOnlyList<IngredienteDoPlano> Ingredientes { get; }

        private PlanoDeBaixa(IReadOnlyList<IngredienteDoPlano> ingredientes) => Ingredientes = ingredientes;

        public IEnumerable<IngredienteDoPlano> ComFalta => Ingredientes.Where(i => i.Situacao == SituacaoNoPlano.Falta);
        public IEnumerable<IngredienteDoPlano> SoComVencido => Ingredientes.Where(i => i.Situacao == SituacaoNoPlano.SoVencido);
        public bool TemProblema => Ingredientes.Any(i => i.Situacao != SituacaoNoPlano.Ok);

        /// <summary>Todas as retiradas, na ordem dos ingredientes e, dentro de cada um, na ordem de uso (FEFO).</summary>
        public IEnumerable<(IngredienteDoPlano Ingrediente, LoteInsumo Lote, decimal Quantidade)> Retiradas =>
            Ingredientes.SelectMany(i => i.Retiradas.Select(r => (i, r.Lote, r.Quantidade)));

        /// <param name="itens">Ingredientes na ordem em que devem aparecer.</param>
        /// <param name="lotes">Lotes com saldo dos insumos da receita (podem incluir vencidos).</param>
        public static PlanoDeBaixa Planejar(
            IEnumerable<ReceitaItem> itens, decimal volumeBaseLitros, decimal litros, IEnumerable<LoteInsumo> lotes, DateOnly hoje)
        {
            var porInsumo = lotes.ToLookup(l => l.InsumoId);
            var resultado = new List<IngredienteDoPlano>();

            foreach (var item in itens)
            {
                var necessario = CalculoProducao.Escalar(item.Quantidade, volumeBaseLitros, litros);
                var doInsumo = porInsumo[item.InsumoId].ToList();
                var fila = CalculoProducao.FilaFefo(doInsumo, hoje);
                var disponivel = fila.Sum(l => l.QuantidadeAtual);
                var vencidos = CalculoProducao.OrdemDeUso(doInsumo.Where(l => l.QuantidadeAtual > 0 && !CalculoProducao.PodeSerConsumido(l, hoje))).ToList();

                if (disponivel >= necessario)
                {
                    resultado.Add(new IngredienteDoPlano(item, necessario, disponivel, SituacaoNoPlano.Ok,
                        CalculoProducao.Alocar(fila, necessario), vencidos));
                    continue;
                }

                // Faltou: nada sai deste ingrediente. Sem lote utilizável e com saldo vencido, é "só vencido".
                var situacao = fila.Count == 0 && vencidos.Count > 0 ? SituacaoNoPlano.SoVencido : SituacaoNoPlano.Falta;
                resultado.Add(new IngredienteDoPlano(item, necessario, disponivel, situacao,
                    Array.Empty<(LoteInsumo, decimal)>(), vencidos));
            }

            return new PlanoDeBaixa(resultado);
        }
    }
}
