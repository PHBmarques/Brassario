using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record AlertaResponse(
        TipoAlerta Tipo,
        SeveridadeAlerta Severidade,
        long InsumoId,
        string InsumoNome,
        CategoriaInsumo Categoria,
        UnidadeMedida UnidadeMedida,
        long? LoteId,
        string? CodigoLote,
        DateOnly? DataValidade,
        int? DiasParaVencer,
        decimal Saldo,
        decimal EstoqueMinimo,
        string Mensagem);
}
