using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record MovimentacaoResponse(
        long Id,
        long LoteId,
        string CodigoLote,
        long InsumoId,
        string InsumoNome,
        long UsuarioId,
        TipoMovimentacao Tipo,
        decimal Quantidade,
        string? Motivo,
        long? BateladaId,
        DateTime DataHora);
}
