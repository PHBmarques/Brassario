namespace Brassario.Api.DTOs.Request
{
    public class PrimeiroAcessoRequest
    {
        public string NovaSenha { get; set; } = string.Empty;
        public string ConfirmarSenha { get; set; } = string.Empty;

        /// <summary>Opcional. Se vier, recebe o link de confirmação.</summary>
        public string? EmailRecuperacao { get; set; }

        public override string ToString() => $"PrimeiroAcessoRequest {{ EmailRecuperacao = {EmailRecuperacao}, Senhas = *** }}";
    }
}
