using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class CancelarBateladaRequest
    {
        [Required(ErrorMessage = "Informe o motivo do cancelamento.")]
        [StringLength(255, MinimumLength = 5, ErrorMessage = "O motivo deve ter entre 5 e 255 caracteres.")]
        public string? Motivo { get; set; }
    }
}
