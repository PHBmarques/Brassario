namespace Brassario.Api.DTOs.Response
{
    /// <summary>Resultado da entrada: o lote, o insumo e o fornecedor (os novos já com id) e o saldo do insumo depois da entrada.</summary>
    /// <param name="SaldoAtual">Saldo do insumo depois da entrada, só com lotes dentro da validade e com saldo (igual ao armário).</param>
    public record EntradaResponse(LoteResponse Lote, InsumoResponse Insumo, FornecedorResponse? Fornecedor, decimal SaldoAtual);
}
