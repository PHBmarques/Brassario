using Brassario.Api.Models;

namespace Brassario.Api.Services
{
    public static class CalculoProducao
    {
        /// <summary>
        /// RN-04: necessário = quantidade do item x (volume planejado / volume base da receita).
        /// Arredondado para 3 casas, como o saldo dos lotes no banco.
        /// </summary>
        public static decimal Escalar(decimal quantidadeBase, decimal volumeBase, decimal volume) =>
            Math.Round(quantidadeBase * volume / volumeBase, 3, MidpointRounding.AwayFromZero);

        /// <summary>
        /// Lote que pode ser consumido: ainda tem saldo e não está vencido (validade de hoje em diante).
        /// É a única definição desta regra: a baixa, o armário, a busca e a entrada usam esta.
        /// </summary>
        public static bool PodeSerConsumido(LoteInsumo lote, DateOnly hoje) =>
            lote.DataValidade >= hoje && lote.QuantidadeAtual > 0;

        /// <summary>
        /// ORDEM DE USO (FEFO): validade mais próxima primeiro; empate de validade, quem entrou primeiro; depois o menor id.
        /// É a única definição desta ordem: a baixa de estoque (batelada), a previsão de disponibilidade
        /// e as telas do armário usam esta.
        /// </summary>
        public static IOrderedEnumerable<LoteInsumo> OrdemDeUso(IEnumerable<LoteInsumo> lotes) =>
            lotes.OrderBy(l => l.DataValidade).ThenBy(l => l.DataEntrada).ThenBy(l => l.Id);

        /// <summary>
        /// Fila FEFO (RN-02 e RN-03): só lotes com saldo e não vencidos, na ordem de uso.
        /// </summary>
        public static List<LoteInsumo> FilaFefo(IEnumerable<LoteInsumo> lotes, DateOnly hoje) =>
            OrdemDeUso(lotes.Where(l => PodeSerConsumido(l, hoje))).ToList();

        /// <summary>
        /// Distribui a quantidade necessária pela fila FEFO: consome o lote inteiro e passa para o próximo.
        /// Se a fila não basta, devolve só o que existe (a falta é calculada por quem chama).
        /// </summary>
        public static List<(LoteInsumo Lote, decimal Quantidade)> Alocar(IEnumerable<LoteInsumo> fila, decimal necessario)
        {
            var resultado = new List<(LoteInsumo, decimal)>();
            var restante = necessario;

            foreach (var lote in fila)
            {
                if (restante <= 0) break;

                var retirar = Math.Min(restante, lote.QuantidadeAtual);
                if (retirar <= 0) continue;

                resultado.Add((lote, retirar));
                restante -= retirar;
            }

            return resultado;
        }

        /// <summary>Custo de um consumo: quantidade x preço unitário do lote (4 casas, como no banco).</summary>
        public static decimal Custo(decimal quantidade, decimal precoUnitario) =>
            Math.Round(quantidade * precoUnitario, 4, MidpointRounding.AwayFromZero);

        /// <summary>Custo total da batelada: soma dos custos dos consumos, com 2 casas.</summary>
        public static decimal CustoTotal(IEnumerable<decimal> custos) =>
            Math.Round(custos.Sum(), 2, MidpointRounding.AwayFromZero);
    }
}
