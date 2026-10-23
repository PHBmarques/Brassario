using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Models
{
    [Table("lote_insumo")]
    public class LoteInsumo
    {
        public long Id { get; set; }

        public long InsumoId { get; set; }
        public Insumo Insumo { get; set; } = null!;

        public long? FornecedorId { get; set; }
        public Fornecedor? Fornecedor { get; set; }

        // Código do lote que vem do fornecedor ou da nota fiscal
        [Required, MaxLength(60)]
        public string CodigoLote { get; set; } = string.Empty;

        [MaxLength(30)]
        public string? NotaFiscal { get; set; }

        public DateOnly DataEntrada { get; set; }

        public DateOnly DataValidade { get; set; }

        [Precision(12, 3)]
        public decimal QuantidadeInicial { get; set; }

        // Saldo atual do lote. Nunca pode ficar negativo (CHECK no banco, configurado no BD-2)
        [Precision(12, 3)]
        public decimal QuantidadeAtual { get; set; }

        [Precision(12, 4)]
        public decimal PrecoUnitario { get; set; }

        // Controle de concorrência adicional: incrementar a cada alteração de saldo
        [ConcurrencyCheck]
        public long Versao { get; set; }

        public ICollection<BateladaConsumo> Consumos { get; set; } = new List<BateladaConsumo>();
        public ICollection<MovimentacaoEstoque> Movimentacoes { get; set; } = new List<MovimentacaoEstoque>();
    }
}
