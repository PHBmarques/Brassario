using Brassario.Api.Models;

namespace Brassario.Api.Services
{
    /// <summary>O que foi gravado numa entrada: o lote e o insumo e o fornecedor dele (já com os ids gerados).</summary>
    public record EntradaGravada(LoteInsumo Lote, Insumo Insumo, Fornecedor? Fornecedor);
}
