namespace Brassario.Api.Services
{
    public class SegurancaOptions
    {
        public const string Secao = "Seguranca";

        /// <summary>Pedidos de "esqueci minha senha" por hora, por e-mail de login e por IP.</summary>
        public int EsqueciSenhaPorHora { get; set; } = 3;
    }
}