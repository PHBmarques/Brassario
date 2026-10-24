using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;

namespace Brassario.Api.Services
{
    public interface ILoteService
    {
        /// <summary>Registra o recebimento de um insumo: cria o lote e a movimentação ENTRADA na mesma transação.</summary>
        Task<LoteResponse> RegistrarEntradaAsync(LoteRequest request, long usuarioId, CancellationToken ct = default);

        /// <summary>Só confere os campos do lote (400 por campo). Sem exigir o insumo quando ele vem como "novo".</summary>
        void ValidarEntrada(LoteRequest request, bool exigirInsumo = true);

        /// <summary>
        /// Entrada composta: o insumo e/ou o fornecedor podem ser novos (ainda não gravados). Tudo é gravado
        /// em UM único SaveChanges, então ou grava insumo, fornecedor, lote e movimentação, ou não grava nada.
        /// Com insumoNovo, o InsumoId do request é ignorado; com fornecedorNovo, o FornecedorId é ignorado.
        /// </summary>
        Task<EntradaGravada> RegistrarEntradaCompostaAsync(
            LoteRequest request, Brassario.Api.Models.Insumo? insumoNovo, Brassario.Api.Models.Fornecedor? fornecedorNovo,
            long usuarioId, CancellationToken ct = default);
    }
}
