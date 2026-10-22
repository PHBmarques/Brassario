using Brassario.Api.Services;

namespace Brassario.Tests
{
    /// <summary>E-mail de mentira para os testes: guarda o que seria enviado em vez de mandar.</summary>
    public class EmailFalso : IEmailService
    {
        public List<(string Para, string Assunto, string Corpo)> Enviados { get; } = new();

        public Task<bool> EnviarAsync(string para, string assunto, string corpoHtml, CancellationToken ct = default)
        {
            Enviados.Add((para, assunto, corpoHtml));
            return Task.FromResult(true);
        }
    }
}