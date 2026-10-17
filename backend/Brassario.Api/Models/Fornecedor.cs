using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Brassario.Api.Models
{
    [Table("fornecedor")]
    public class Fornecedor
    {
        public long Id { get; set; }

        [Required, MaxLength(120)]
        public string Nome { get; set; } = string.Empty;

        [MaxLength(160)]
        public string? Contato { get; set; }

        public bool Ativo { get; set; } = true;

        public ICollection<LoteInsumo> Lotes { get; set; } = new List<LoteInsumo>();
    }
}
