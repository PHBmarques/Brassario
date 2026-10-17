using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Models
{

    [Table("insumo")]
    public class Insumo
    {
        public long Id { get; set; }

        [Required, MaxLength(120)]
        public string Nome { get; set; } = string.Empty;

        public CategoriaInsumo Categoria { get; set; }

        public UnidadeMedida UnidadeMedida { get; set; }

        // Abaixo deste saldo, o Armário mostra alerta de escassez
        [Precision(12, 3)]
        public decimal EstoqueMinimo { get; set; }

        // Dias para vencer que marcam Validade Crítica (vermelho)
        public int DiasValidadeCritica { get; set; } = 15;

        // Dias para vencer que marcam atenção (amarelo)
        public int DiasValidadeAtencao { get; set; } = 45;

        public bool Ativo { get; set; } = true;

        public ICollection<LoteInsumo> Lotes { get; set; } = new List<LoteInsumo>();
        public ICollection<ReceitaItem> ReceitaItens { get; set; } = new List<ReceitaItem>();
    }
}
