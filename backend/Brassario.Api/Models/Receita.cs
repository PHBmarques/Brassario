using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Models
{
    [Table("receita")]
    public class Receita
    {
        public long Id { get; set; }

        [Required, MaxLength(120)]
        public string Nome { get; set; } = string.Empty;

        [MaxLength(80)]
        public string? Estilo { get; set; }

        [Precision(10, 2)]
        public decimal VolumeBaseLitros { get; set; }

        public string? ModoPreparo { get; set; }

        public bool Ativa { get; set; } = true;

        public ICollection<ReceitaItem> Itens { get; set; } = new List<ReceitaItem>();
        public ICollection<Batelada> Bateladas { get; set; } = new List<Batelada>();
    }
}
