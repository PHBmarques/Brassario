using System.Net;

namespace Brassario.Api.Services
{
    public static class EmailModelos
    {
        public static string ConfirmarEmail(string nome, string link) => Montar(
            nome,
            "Você cadastrou este e-mail para recuperar a senha do Brassário. Confirme para ativar a recuperação.",
            "Confirmar e-mail", link,
            "O link vale por 24 horas. Se não foi você, ignore esta mensagem.");

        public static string RedefinirSenha(string nome, string emailLogin, string link) => Montar(
            nome,
            $"Recebemos um pedido para redefinir a senha da conta <strong>{WebUtility.HtmlEncode(emailLogin)}</strong>.",
            "Criar nova senha", link,
            "O link vale por 30 minutos e só pode ser usado uma vez. Se você não pediu, ignore este e-mail: sua senha continua a mesma.");

        private static string Montar(string nome, string corpo, string textoBotao, string link, string rodape)
        {
            var linkSeguro = WebUtility.HtmlEncode(link);
            // O logo fica no próprio site (frontend/public/logo-brassario.png): usa o mesmo endereço do link.
            var logo = Uri.TryCreate(link, UriKind.Absolute, out var uri)
                ? WebUtility.HtmlEncode($"{uri.GetLeftPart(UriPartial.Authority)}/logo-brassario.png")
                : null;
            var imagemLogo = logo is null
                ? ""
                : $"""<img src="{logo}" alt="" width="40" height="40" style="display:block;border:0" />""";

            return $"""
            <div style="background:#e8ebe4;padding:32px 16px;font-family:Arial,Helvetica,sans-serif;color:#1d2b22">
              <table role="presentation" align="center" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;margin:0 auto;background:#ffffff;border:1px solid #dfe5d8;border-radius:8px">
                <tr><td style="padding:28px 32px">
                  <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 24px">
                    <tr>
                      <td style="padding-right:10px;vertical-align:middle">{imagemLogo}</td>
                      <td style="vertical-align:middle;font-size:20px;font-weight:bold;color:#0b6b47">Brassário</td>
                    </tr>
                  </table>
                  <p style="margin:0 0 12px;font-size:14px;font-weight:bold">Olá, {WebUtility.HtmlEncode(nome)}.</p>
                  <p style="margin:0 0 24px;font-size:14px;line-height:1.5">{corpo}</p>
                  <p style="margin:0 0 24px"><a href="{linkSeguro}" style="display:inline-block;background:#0b6b47;color:#ffffff;text-decoration:none;font-size:14px;font-weight:bold;padding:12px 24px;border-radius:8px">{textoBotao}</a></p>
                  <p style="margin:0;font-size:12px;line-height:1.5;color:#55655b">{rodape}</p>
                </td></tr>
              </table>
            </div>
            """;
        }
    }
}