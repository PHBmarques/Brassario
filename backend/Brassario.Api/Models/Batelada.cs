using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Models
{
    [Table("batelada")]
    public class Batelada
    {
        public long Id { get; set; }

        [Required, MaxLength(20)]
        public string Codigo { get; set; } = string.Empty;

        public long ReceitaId { get; set; }
        public Receita Receita { get; set; } = null!;

        public long UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Precision(10, 2)]
        public decimal VolumePlanejado { get; set; }

        [Precision(10, 2)]
        public decimal? VolumeFinal { get; set; }

        public StatusBatelada Status { get; set; } = StatusBatelada.EM_ANDAMENTO;

        [Precision(14, 2)]
        public decimal CustoTotal { get; set; }

        public DateTime DataProducao { get; set; } = DateTime.UtcNow;

        /// <summary>Por que a produção foi cancelada. Nulo fora do cancelamento (CHECK ck_batelada_cancelamento).</summary>
        [MaxLength(255)]
        public string? MotivoCancelamento { get; set; }

        /// <summary>Quando a produção foi cancelada (UTC). Nulo fora do cancelamento.</summary>
        public DateTime? CanceladoEm { get; set; }

        [MaxLength(255)]
        public string? Observacoes { get; set; }

        public ICollection<BateladaConsumo> Consumos { get; set; } = new List<BateladaConsumo>();
        public ICollection<MovimentacaoEstoque> Movimentacoes { get; set; } = new List<MovimentacaoEstoque>();
    }
}
