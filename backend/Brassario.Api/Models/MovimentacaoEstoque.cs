using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;


namespace Brassario.Api.Models
{
    [Table("movimentacao_estoque")]
    public class MovimentacaoEstoque
    {
        public long Id { get; set; }

        public long LoteInsumoId { get; set; }
        public LoteInsumo LoteInsumo { get; set; } = null!;

        public long UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public long? BateladaId { get; set; }
        public Batelada? Batelada { get; set; }

        public TipoMovimentacao Tipo { get; set; }

        [Precision(12, 3)]
        public decimal Quantidade { get; set; }

        [MaxLength(255)]
        public string? Motivo { get; set; }

        public DateTime DataHora { get; set; } = DateTime.UtcNow;
    }
}
