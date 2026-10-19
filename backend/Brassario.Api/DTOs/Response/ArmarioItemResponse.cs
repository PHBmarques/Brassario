using Brassario.Api.Models;

namespace Brassario.Api.DTOs.Response
{
    public record ArmarioItemResponse(
    long InsumoId,
    string Nome,
    CategoriaInsumo Categoria,
    UnidadeMedida UnidadeMedida,
    bool Ativo,
    decimal EstoqueMinimo,
    /// <summary>Soma dos saldos dos lotes NÃO vencidos.</summary>
    decimal Saldo,
    StatusEscassez StatusEscassez,
    int QuantidadeLotes,
    /// <summary>Validade do lote não vencido que vence primeiro. Nulo quando não há saldo.</summary>
    DateOnly? ProximoVencimento,
    int? DiasParaVencer,
    /// <summary>Cor do próximo vencimento, pelos limites do insumo (RN-10 e RN-11). Nula quando não há saldo.</summary>
    SituacaoValidade? Cor);
    
}
