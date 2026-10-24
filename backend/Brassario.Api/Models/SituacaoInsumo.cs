namespace Brassario.Api.Models
{
    /// <summary>
    /// Situação do insumo no armário. Quando mais de uma vale, aparece a primeira desta ordem:
    /// VENCIDO, CRITICO, ABAIXO_DO_MINIMO, ATENCAO, OK.
    /// </summary>
    public enum SituacaoInsumo
    {
        /// <summary>Estoque em dia: validade longe e saldo igual ou acima do mínimo.</summary>
        OK,

        /// <summary>O lote que vence primeiro está na faixa de atenção (DiasValidadeAtencao).</summary>
        ATENCAO,

        /// <summary>O lote que vence primeiro está na faixa crítica (DiasValidadeCritica).</summary>
        CRITICO,

        /// <summary>Há lote com saldo já vencido no armário.</summary>
        VENCIDO,

        /// <summary>Saldo utilizável abaixo do estoque mínimo (inclui insumo sem lote, se tiver mínimo).</summary>
        ABAIXO_DO_MINIMO
    }
}
