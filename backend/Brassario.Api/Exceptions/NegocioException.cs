namespace Brassario.Api.Exceptions
{
    /// <summary>Regra de negócio violada (RN-xx). Vira HTTP 422.</summary>
    public class NegocioException : Exception
    {
        public string Codigo { get; }
        public IDictionary<string, string[]>? Detalhes { get; }

        public NegocioException(string mensagem, string codigo = "REGRA_NEGOCIO", IDictionary<string, string[]>? detalhes = null)
            : base(mensagem)
        {
            Codigo = codigo;
            Detalhes = detalhes;
        }
    }
}
