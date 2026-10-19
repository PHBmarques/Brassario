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
    }
}
