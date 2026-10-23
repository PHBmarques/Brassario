namespace Brassario.Api.Services
{
    public class SegurancaOptions
    {
        public const string Secao = "Seguranca";

        /// <summary>Pedidos de "esqueci minha senha" por hora, por e-mail de login e por IP.</summary>
        public int EsqueciSenhaPorHora { get; set; } = 3;

        /// <summary>Reenvios do link de confirmação do e-mail de recuperação por hora, por usuário.</summary>
        public int ReenviarConfirmacaoPorHora { get; set; } = 3;
    }
}