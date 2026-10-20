using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class BateladaRequest
    {
        /// <summary>Id da receita (precisa estar ativa).</summary>
        [Range(1, long.MaxValue, ErrorMessage = "Informe a receita.")]
        public long ReceitaId { get; set; }

        /// <summary>Volume a produzir, em litros (maior que zero, até 2 casas decimais).</summary>
        [Required(ErrorMessage = "Informe o volume a produzir, em litros.")]
        [Range(0.01, 99999999, ErrorMessage = "O volume deve ser maior que zero (mínimo 0,01 litro).")]
        public decimal? VolumePlanejado { get; set; }

        /// <summary>Observação opcional (até 255 caracteres).</summary>
        [StringLength(255, ErrorMessage = "As observações têm no máximo 255 caracteres.")]
        public string? Observacoes { get; set; }
    }
}
