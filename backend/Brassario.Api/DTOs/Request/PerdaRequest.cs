using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class PerdaRequest
    {
        [Range(typeof(decimal), "0.001", "999999999.999", ErrorMessage = "A quantidade deve ser maior que zero.")]
        public decimal Quantidade { get; set; }

        [Required(ErrorMessage = "O motivo é obrigatório."), StringLength(255, MinimumLength = 5,
            ErrorMessage = "O motivo deve ter entre 5 e 255 caracteres.")]
        public string Motivo { get; set; } = string.Empty;
    }
}

