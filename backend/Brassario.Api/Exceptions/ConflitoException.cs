namespace Brassario.Api.Exceptions
{
    /// <summary>Duplicidade ou registro em uso (e-mail repetido, SKU repetido). Vira HTTP 409.</summary>
    public class ConflitoException: Exception
    {
        public ConflitoException(string mensagem) : base(mensagem) { }
    }
}
