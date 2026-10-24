namespace Brassario.Api.Exceptions
{
    /// <summary>
    /// A produção não pode seguir porque os únicos lotes com saldo de um insumo estão vencidos
    /// (lote vencido nunca é consumido). Responde 422 LOTE_VENCIDO.
    /// </summary>
    public class LoteVencidoException : NegocioException
    {
        public LoteVencidoException(IReadOnlyCollection<(string Insumo, string Lotes)> insumos)
            : base(
                insumos.Count == 1
                    ? $"Todos os lotes em estoque de {insumos.First().Insumo} estão vencidos e não podem ser usados."
                    : $"Os únicos lotes em estoque de {insumos.Count} insumo(s) estão vencidos: {string.Join(", ", insumos.Select(i => i.Insumo))}.",
                "LOTE_VENCIDO",
                insumos.ToDictionary(i => i.Insumo, i => new[] { $"só há lote vencido: {i.Lotes}" }))
        {
        }
    }
}
