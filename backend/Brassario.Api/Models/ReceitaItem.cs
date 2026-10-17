using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Models
{
    [Table("receita_item")]
    public class ReceitaItem
    {
        public long Id { get; set; }

        public long ReceitaId { get; set; }
        public Receita Receita { get; set; } = null!;

        public long InsumoId { get; set; }
        public Insumo Insumo { get; set; } = null!;

        [Precision(12, 3)]
        public decimal Quantidade { get; set; }
    }
}
