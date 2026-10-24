namespace Brassario.Api.Exceptions
{
    /// <summary>Duplicidade ou registro em uso (e-mail repetido, nome repetido, código de lote repetido). Vira HTTP 409.</summary>
    public class ConflitoException : Exception
    {
        /// <summary>Código devolvido no corpo do erro. Padrão "CONFLITO"; casos específicos usam o seu (ex.: NOME_DUPLICADO).</summary>
        public string Codigo { get; }

        /// <summary>Campo(s) com o problema, no mesmo formato dos erros 400, para a tela mostrar a mensagem embaixo do campo.</summary>
        public IDictionary<string, string[]>? Detalhes { get; }

        public ConflitoException(string mensagem, string codigo = "CONFLITO", IDictionary<string, string[]>? detalhes = null)
            : base(mensagem)
        {
            Codigo = codigo;
            Detalhes = detalhes;
        }

        /// <summary>409 NOME_DUPLICADO apontando para o campo (padrão "nome").</summary>
        public static ConflitoException NomeDuplicado(string mensagem, string campo = "nome") =>
            new(mensagem, "NOME_DUPLICADO", new Dictionary<string, string[]> { [campo] = new[] { mensagem } });
    }
}
