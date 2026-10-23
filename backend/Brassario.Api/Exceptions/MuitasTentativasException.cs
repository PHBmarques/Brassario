namespace Brassario.Api.Exceptions
{
    /// <summary>Limite de tentativas estourado. Vira HTTP 429.</summary>
    public class MuitasTentativasException : Exception
    {
        public MuitasTentativasException(string mensagem = "Muitas tentativas. Aguarde um pouco e tente novamente.")
            : base(mensagem) { }
    }
}