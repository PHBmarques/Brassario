namespace Brassario.Api.DTOs.Request
{
    public class RedefinirSenhaRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NovaSenha { get; set; } = string.Empty;
        public string ConfirmarSenha { get; set; } = string.Empty;

        public override string ToString() => "RedefinirSenhaRequest { Token = ***, Senhas = *** }";
    }
}