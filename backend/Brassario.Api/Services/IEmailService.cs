namespace Brassario.Api.Services
{
    public interface IEmailService
    {
        // Retorna false se o envio falhar. Nunca lança exceção para quem chamou.
        Task<bool> EnviarAsync(string para, string assunto, string corpoHtml,
            CancellationToken ct = default);
    }
}
