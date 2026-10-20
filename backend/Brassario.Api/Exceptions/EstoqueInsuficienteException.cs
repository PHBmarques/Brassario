namespace Brassario.Api.Exceptions
{
    /// <summary>Não há saldo suficiente de um insumo (baixa FEFO, produzir batelada).
    public class EstoqueInsuficienteException : NegocioException
    {
        public EstoqueInsuficienteException(string insumo, decimal necessario, decimal disponivel)
        : base(
            $"Estoque insuficiente de {insumo}: necessário {necessario}, disponível {disponivel}.",
            "ESTOQUE_INSUFICIENTE",
            new Dictionary<string, string[]>
            {
                [insumo] = new[] { $"necessário {necessario}, disponível {disponivel}" }
            })
        {
        }

        /// <summary>Produção com mais de um insumo em falta: lista todos de uma vez.</summary>
        public EstoqueInsuficienteException(IReadOnlyCollection<(string Insumo, decimal Necessario, decimal Disponivel)> faltas)
            : base(
                $"Estoque insuficiente para {faltas.Count} insumo(s): {string.Join(", ", faltas.Select(f => f.Insumo))}.",
                "ESTOQUE_INSUFICIENTE",
                faltas.ToDictionary(
                    f => f.Insumo,
                    f => new[] { $"necessário {f.Necessario}, disponível {f.Disponivel}, falta {f.Necessario - f.Disponivel}" }))
        {
        }
    }
}
