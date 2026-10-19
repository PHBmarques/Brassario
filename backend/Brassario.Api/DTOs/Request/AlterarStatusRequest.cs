using System.ComponentModel.DataAnnotations;


namespace Brassario.Api.DTOs.Request
{
    /// <summary>Ativa ou inativa um registro (RN-16: inativa em vez de excluir).</summary>
    public class AlterarStatusRequest
    {
        [Required(ErrorMessage = "Informe se o registro fica ativo ou inativo.")]
        public bool? Ativo { get; set; }
    }
}
