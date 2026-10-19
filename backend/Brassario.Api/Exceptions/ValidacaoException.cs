namespace Brassario.Api.Exceptions
{
    /// <summary>Validação feita na camada de serviço, com o campo de cada erro. Vira HTTP 400.</summary>
    public class ValidacaoException: Exception
    {
        public IDictionary<string, string[]> Detalhes { get; }

        public ValidacaoException(IDictionary<string, string[]> detalhes)
            : base("Dados inválidos. Verifique os campos informados.")
        {
            Detalhes = detalhes;
        }

        public ValidacaoException(string campo, string mensagem)
            : this(new Dictionary<string, string[]> { [campo] = new[] { mensagem } }) { }
    }
}
