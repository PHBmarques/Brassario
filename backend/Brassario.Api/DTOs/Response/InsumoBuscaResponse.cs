using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    /// <summary>Item leve da caixa de busca de insumos. SaldoAtual considera só lotes dentro da validade e com saldo (igual ao armário).</summary>
    public record InsumoBuscaResponse(long Id, string Nome, UnidadeMedida UnidadeMedida, decimal SaldoAtual);
}
