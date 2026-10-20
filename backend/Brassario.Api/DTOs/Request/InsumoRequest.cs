using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class InsumoRequest
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        /// <summary>MALTE, LUPULO, LEVEDURA, ADJUNTO ou OUTRO.</summary>
        [Required(ErrorMessage = "A categoria é obrigatória.")]
        public string Categoria { get; set; } = string.Empty;

        /// <summary>KG, G, L, ML ou UN.</summary>
        [Required(ErrorMessage = "A unidade de medida é obrigatória.")]
        public string UnidadeMedida { get; set; } = string.Empty;

        [Range(0, 999999999, ErrorMessage = "O estoque mínimo não pode ser negativo.")]
        public decimal EstoqueMinimo { get; set; }

        /// <summary>Dias para vencer que marcam Validade Crítica (vermelho). Padrão: 15.</summary>
        [Range(0, 3650, ErrorMessage = "Os dias de validade crítica devem estar entre 0 e 3650.")]
        public int? DiasValidadeCritica { get; set; }

        /// <summary>Dias para vencer que marcam atenção (amarelo). Padrão: 45.</summary>
        [Range(0, 3650, ErrorMessage = "Os dias de validade de atenção devem estar entre 0 e 3650.")]
        public int? DiasValidadeAtencao { get; set; }
    }
}
