using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Request
{
    public class MovimentacaoFiltro
    {
        public TipoMovimentacao? Tipo { get; set; }
        public long? LoteId { get; set; }
        public long? InsumoId { get; set; }
        public long? UsuarioId { get; set; }
        public long? BateladaId { get; set; }
        public DateOnly? DataInicio { get; set; }
        public DateOnly? DataFim { get; set; }
    }
}
