using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>
    /// Um cartão da tela Alertas. Traz os ids que a ação do cartão precisa para abrir a tela certa:
    /// insumoId sempre; loteId nos alertas de lote (nulo em ESTOQUE_VAZIO e ESTOQUE_BAIXO).
    /// Grupos da tela: severidade CRITICA e ALTA = "Urgente"; MEDIA e BAIXA = "Atenção".
    /// </summary>
    public record AlertaResponse(
        TipoAlerta Tipo,
        SeveridadeAlerta Severidade,
        AcaoAlerta Acao,
        long InsumoId,
        string InsumoNome,
        CategoriaInsumo Categoria,
        UnidadeMedida UnidadeMedida,
        long? LoteId,
        string? CodigoLote,
        DateOnly? DataValidade,
        /// <summary>Negativo quando já venceu. Nulo nos alertas de estoque.</summary>
        int? DiasParaVencer,
        /// <summary>Saldo do lote nos alertas de lote; saldo utilizável do insumo nos alertas de estoque.</summary>
        decimal Saldo,
        decimal EstoqueMinimo,
        string Mensagem);
}
