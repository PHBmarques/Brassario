using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class ReceitaRequest
    {
        [Required(ErrorMessage = "O nome é obrigatório.")]
        [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
        public string Nome { get; set; } = string.Empty;

        /// <summary>Estilo da cerveja (ex.: IPA, Pilsen, Witbier). Opcional.</summary>
        [StringLength(80, ErrorMessage = "O estilo deve ter no máximo 80 caracteres.")]
        public string? Estilo { get; set; }

        /// <summary>Volume, em litros, para o qual as quantidades dos ingredientes foram definidas.</summary>
        [Range(0.01, 99999999, ErrorMessage = "O volume base deve ser maior que zero.")]
        public decimal VolumeBaseLitros { get; set; }

        [StringLength(5000, ErrorMessage = "O modo de preparo deve ter no máximo 5000 caracteres.")]
        public string? ModoPreparo { get; set; }

        /// <summary>Ingredientes da receita: pelo menos 1, sem repetir o mesmo insumo.</summary>
        [Required(ErrorMessage = "Informe os ingredientes da receita.")]
        [MinLength(1, ErrorMessage = "A receita precisa ter pelo menos 1 ingrediente.")]
        [MaxLength(100, ErrorMessage = "A receita pode ter no máximo 100 ingredientes.")]
        public List<ReceitaItemRequest> Itens { get; set; } = new();
    }

    /// <summary>Um ingrediente da receita: o insumo e a quantidade para o volume base.</summary>
    public class ReceitaItemRequest
    {
        [Range(1.0, 9.0e18, ErrorMessage = "Informe o insumo do ingrediente.")]
        public long InsumoId { get; set; }

        /// <summary>Quantidade, na unidade de medida do insumo, para o volume base da receita.</summary>
        [Range(0.001, 999999999, ErrorMessage = "A quantidade deve ser maior que zero (mínimo 0,001).")]
        public decimal Quantidade { get; set; }
    }
}
