using System.ComponentModel.DataAnnotations;

namespace Brassario.Api.DTOs.Request
{
    /// <summary>
    /// Entrada de estoque em uma só chamada. Para o insumo, informe o id de um que já existe OU os dados de um novo
    /// (nunca os dois, e nunca nenhum). O fornecedor é opcional: id, dados novos, ou nenhum dos dois.
    /// </summary>
    public class EntradaRequest
    {
        [Required(ErrorMessage = "Informe os dados do lote.")]
        public EntradaLoteRequest? Lote { get; set; }

        /// <summary>Id de um insumo que já existe. Não pode vir junto com novoInsumo.</summary>
        public long? InsumoId { get; set; }

        /// <summary>Dados de um insumo novo (mesmas regras de POST /insumos). Não pode vir junto com insumoId.</summary>
        public InsumoRequest? NovoInsumo { get; set; }

        /// <summary>Id de um fornecedor que já existe. Opcional. Não pode vir junto com novoFornecedor.</summary>
        public long? FornecedorId { get; set; }

        /// <summary>Dados de um fornecedor novo (mesmas regras de POST /fornecedores). Não pode vir junto com fornecedorId.</summary>
        public FornecedorRequest? NovoFornecedor { get; set; }
    }
}
