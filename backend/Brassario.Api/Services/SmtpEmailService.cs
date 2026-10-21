using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Brassario.Api.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailOptions _opcoes;
        private readonly ILogger<SmtpEmailService> _log;

        public SmtpEmailService(IOptions<EmailOptions> opcoes, ILogger<SmtpEmailService> log)
        {
            _opcoes = opcoes.Value;
            _log = log;
        }

        public async Task<bool> EnviarAsync(string para, string assunto, string corpoHtml,
            CancellationToken ct = default)
        {
            try
            {
                var mensagem = new MimeMessage();
                mensagem.From.Add(new MailboxAddress(_opcoes.FromName, _opcoes.From));
                mensagem.To.Add(MailboxAddress.Parse(para));
                mensagem.Subject = assunto;
                mensagem.Body = new BodyBuilder { HtmlBody = corpoHtml }.ToMessageBody();

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_opcoes.Host, _opcoes.Port,
                    _opcoes.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, ct);

                if (!string.IsNullOrWhiteSpace(_opcoes.User))
                    await smtp.AuthenticateAsync(_opcoes.User, _opcoes.Password ?? string.Empty, ct);

                await smtp.SendAsync(mensagem, ct);
                await smtp.DisconnectAsync(true, ct);
                return true;
            }
            catch (Exception ex)
            {
                // Não registra o corpo da mensagem: ele contém o link com o token.
                _log.LogError(ex, "Falha ao enviar e-mail '{Assunto}' para {Destino}", assunto, para);
                return false;
            }
        }
    }
}
