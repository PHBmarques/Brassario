namespace Brassario.Api.Models
{
    /// <summary>Botão do cartão de alerta. O backend decide; a tela só abre a página certa com os ids do alerta.</summary>
    public enum AcaoAlerta
    {
        /// <summary>Abre os lotes do insumo (insumoId, e loteId para destacar o lote).</summary>
        VER_LOTES,

        /// <summary>Abre o registro de perda do lote vencido (loteId).</summary>
        REGISTRAR_PERDA,

        /// <summary>Abre a entrada de estoque do insumo (insumoId; loteId nulo).</summary>
        DAR_ENTRADA,

        /// <summary>Abre as compras sugeridas (insumoId; loteId nulo).</summary>
        VER_COMPRAS
    }
}
