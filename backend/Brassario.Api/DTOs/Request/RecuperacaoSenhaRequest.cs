namespace Brassario.Api.DTOs.Request
{
    /// <summary>Pedido de "esqueci a senha": o e-mail de login ou o e-mail de recuperação.</summary>
    public class EsqueciSenhaRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    /// <summary>Nova senha a partir do link de redefinição enviado por e-mail.</summary>
    public class RedefinirSenhaRequest
    {
        public string Token { get; set; } = string.Empty;
        public string NovaSenha { get; set; } = string.Empty;
        public string ConfirmarSenha { get; set; } = string.Empty;

        public override string ToString() => "RedefinirSenhaRequest { Token = ***, Senhas = *** }";
    }

    /// <summary>Troca de senha pela própria pessoa logada (informando a senha atual).</summary>
    public class AlterarSenhaRequest
    {
        public string SenhaAtual { get; set; } = string.Empty;
        public string NovaSenha { get; set; } = string.Empty;
        public string ConfirmarSenha { get; set; } = string.Empty;

        public override string ToString() => "AlterarSenhaRequest { Senhas = *** }";
    }

    /// <summary>Novo e-mail de recuperação. Recebe um link de confirmação.</summary>
    public class EmailRecuperacaoRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
