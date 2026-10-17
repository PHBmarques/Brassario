using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Models
{
    [Table("batelada_consumo")]
    public class BateladaConsumo
    {
        public long Id { get; set; }

        public long BateladaId { get; set; }
        public Batelada Batelada { get; set; } = null!;

        public long LoteInsumoId { get; set; }
        public LoteInsumo LoteInsumo { get; set; } = null!;

        [Precision(12, 3)]
        public decimal Quantidade { get; set; }

        // Quantidade x preço unitário do lote
        [Precision(14, 4)]
        public decimal Custo { get; set; }
    }
}
