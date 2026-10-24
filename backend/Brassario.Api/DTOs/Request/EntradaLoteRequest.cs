using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    /// <summary>Dados do lote na entrada composta. O insumo e o fornecedor vêm no EntradaRequest (id ou dados novos).</summary>
    public class EntradaLoteRequest
    {
        /// <summary>
        /// Código impresso na embalagem do fornecedor: até 40 caracteres, gravado em maiúsculas e sem espaços nas pontas.
        /// Se vier vazio, o sistema gera L-AAAAMMDD-NN e devolve na resposta.
        /// </summary>
        public string? CodigoLote { get; set; }

        /// <summary>Nº da nota fiscal. Opcional, no máximo 30 caracteres.</summary>
        [StringLength(30, ErrorMessage = "O número da nota fiscal deve ter no máximo 30 caracteres.")]
        public string? NotaFiscal { get; set; }

        /// <summary>Opcional. Padrão: hoje. Não pode ser data futura.</summary>
        public DateOnly? DataEntrada { get; set; }

        /// <summary>Data de validade (formato AAAA-MM-DD). Não pode estar vencida.</summary>
        [Required(ErrorMessage = "A data de validade é obrigatória.")]
        public DateOnly? DataValidade { get; set; }

        [Range(0.001, 999999999, ErrorMessage = "A quantidade deve ser maior que zero.")]
        public decimal Quantidade { get; set; }

        [Range(0, 99999999, ErrorMessage = "O preço unitário não pode ser negativo.")]
        public decimal PrecoUnitario { get; set; }
    }
}
