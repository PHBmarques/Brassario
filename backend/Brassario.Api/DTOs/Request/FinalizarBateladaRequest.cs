using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class FinalizarBateladaRequest
    {
        [Required(ErrorMessage = "Informe o volume final obtido, em litros.")]
        [Range(0.01, 99999999, ErrorMessage = "O volume final deve ser maior que zero.")]
        public decimal? VolumeFinal { get; set; }
    }
}
