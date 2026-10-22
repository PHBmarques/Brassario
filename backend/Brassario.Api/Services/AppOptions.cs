namespace Brassario.Api.Services
{
    public class AppOptions
    {
        public const string Secao = "App";

        /// <summary>Base dos links enviados por e-mail (confirmar e-mail, redefinir senha).</summary>
        public string UrlFront { get; set; } = "http://localhost:3000";
    }
}
