namespace Brassario.Api.Exceptions
{
    /// <summary>O registro pedido não existe. Vira HTTP 404.</summary>
    public class RecursoNaoEncontradoException: Exception
    {
        public RecursoNaoEncontradoException(string mensagem) : base(mensagem) { }

        public RecursoNaoEncontradoException(string recurso, object id)
            : base($"{recurso} não encontrado(a) (id {id}).") { }
    }
}
