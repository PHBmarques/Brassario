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

        public static string RedefinirSenha(string nome, string link) => Montar(
            nome,
            "Recebemos um pedido para criar uma senha nova no Brassário. Clique no botão para escolher a nova senha.",
            "Criar nova senha", link,
            "O link vale por 1 hora e só pode ser usado uma vez. Se não foi você, ignore esta mensagem: sua senha continua a mesma.");

        private static string Montar(string nome, string corpo, string textoBotao, string link, string rodape) => $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#1f2a24">
              <h2 style="color:#0e6b45;margin:0 0 16px">Brassário</h2>
              <p>Olá, {WebUtility.HtmlEncode(nome)}.</p>
              <p>{corpo}</p>
              <p style="margin:24px 0"><a href="{WebUtility.HtmlEncode(link)}" style="background:#0e6b45;color:#ffffff;text-decoration:none;padding:12px 20px;border-radius:8px;display:inline-block">{textoBotao}</a></p>
              <p style="font-size:12px;color:#6b7a70">{rodape}</p>
            </div>
            """;
    }
}