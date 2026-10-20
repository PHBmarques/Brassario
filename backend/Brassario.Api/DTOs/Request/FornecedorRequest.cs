using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class FornecedorRequest
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        /// <summary>E-mail, telefone ou nome do contato. Opcional.</summary>
        [StringLength(160, ErrorMessage = "O contato deve ter no máximo 160 caracteres.")]
        public string? Contato { get; set; }
    }
}
