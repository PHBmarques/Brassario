namespace Brassario.Api.Services
{
    public class EmailOptions
    {
        public const string Secao = "Email";

        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1025;
        public string From { get; set; } = "no-reply@brassario.local";
        public string FromName { get; set; } = "Brassário";

        // Vazios no Mailpit (dev). Em produção vêm de variável de ambiente, nunca do Git.
        public string? User { get; set; }
        public string? Password { get; set; }
        public bool UseSsl { get; set; }
    }
}
