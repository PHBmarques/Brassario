namespace Brassario.Api.DTOs.Request
{
    public class EsqueciSenhaRequest
    {
        /// <summary>E-mail de login (não o de recuperação).</summary>
        public string Email { get; set; } = string.Empty;
    }
}