namespace Brassario.Api.Exceptions
{
    /// <summary>Login bloqueado depois de várias senhas erradas seguidas. Vira HTTP 429 LOGIN_BLOQUEADO.</summary>
    public class LoginBloqueadoException : Exception
    {
        public int MinutosRestantes { get; }

        public LoginBloqueadoException(TimeSpan restante)
            : base(Montar(restante))
        {
            MinutosRestantes = Math.Max(1, (int)Math.Ceiling(restante.TotalMinutes));
        }

        private static string Montar(TimeSpan restante)
        {
            var minutos = Math.Max(1, (int)Math.Ceiling(restante.TotalMinutes));
            return $"Muitas tentativas de login com senha incorreta. Tente novamente em {minutos} minuto{(minutos == 1 ? "" : "s")}.";
        }
    }
}
