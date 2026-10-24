namespace Brassario.Api.Exceptions
{
    /// <summary>Duplicidade ou registro em uso (e-mail repetido, SKU repetido). Vira HTTP 409.</summary>
    public class ConflitoException : Exception
    {
        /// <summary>Código devolvido no corpo do erro. Padrão "CONFLITO"; casos específicos usam o seu (ex.: CODIGO_LOTE_DUPLICADO).</summary>
        public string Codigo { get; }

        public ConflitoException(string mensagem, string codigo = "CONFLITO") : base(mensagem) => Codigo = codigo;
    }
}
