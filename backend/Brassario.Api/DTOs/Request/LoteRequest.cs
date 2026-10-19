using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    public class LoteRequest
    {
        [Range(1, long.MaxValue, ErrorMessage = "Informe o insumo.")]
        public long InsumoId { get; set; }

        /// <summary>Opcional. Quando informado, o fornecedor precisa existir e estar ativo.</summary>
        public long? FornecedorId { get; set; }

        /// <summary>Código do lote do fornecedor ou da nota fiscal. Não pode repetir para o mesmo insumo.</summary>
        [Required(ErrorMessage = "O código do lote é obrigatório.")]
        [StringLength(60, ErrorMessage = "O código do lote deve ter no máximo 60 caracteres.")]
        public string CodigoLote { get; set; } = string.Empty;

        /// <summary>Opcional. Padrão: hoje. Não pode ser data futura.</summary>
        public DateOnly? DataEntrada { get; set; }

        /// <summary>Data de validade (formato AAAA-MM-DD). Não pode estar vencida.</summary>
        [Required(ErrorMessage = "A data de validade é obrigatória.")]
        public DateOnly? DataValidade { get; set; }

        [Range(0.001, 999999999, ErrorMessage = "A quantidade deve ser maior que zero.")]
        public decimal Quantidade { get; set; }

        [Range(typeof(decimal), "0", "99999999", ErrorMessage = "O preço unitário não pode ser negativo.")]
        public decimal PrecoUnitario { get; set; }
    }
}
