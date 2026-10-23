namespace Brassario.Api.DTOs.Request
{
    public class AlterarSenhaRequest
    {
        public string SenhaAtual { get; set; } = string.Empty;
        public string NovaSenha { get; set; } = string.Empty;
        public string ConfirmarSenha { get; set; } = string.Empty;

        public override string ToString() => "AlterarSenhaRequest { Senhas = *** }";
    }
}