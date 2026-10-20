using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;
namespace Brassario.Tests
{
    public class RefreshHttpTests : IClassFixture<SegurancaHttpTests.ApiFalsa>
    {
        private const string NomeCookie = "brassario_refresh";

        private readonly SegurancaHttpTests.ApiFalsa _api;

        public RefreshHttpTests(SegurancaHttpTests.ApiFalsa api) => _api = api;

        // ---------- Apoio ----------

        private static StringContent Json(object corpo) =>
            new(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");

        private static async Task<JsonElement> Ler(HttpResponseMessage r) =>
            JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

        // Linha completa do Set-Cookie do refresh token (ou null se a resposta não mandou esse cookie)
        private static string? LinhaDoCookie(HttpResponseMessage r) =>
            r.Headers.TryGetValues("Set-Cookie", out var valores)
                ? valores.FirstOrDefault(v => v.StartsWith(NomeCookie + "=", StringComparison.Ordinal))
                : null;

        private static string ValorDoCookie(string linha) =>
            linha[(NomeCookie.Length + 1)..].Split(';')[0];

        private async Task<(string AccessToken, string Cookie)> EntrarAsync(string email = "admin@brassario.com", string senha = "Admin@2026")
        {
            var r = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email, senha }));
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var linha = LinhaDoCookie(r);
            Assert.NotNull(linha);
            return ((await Ler(r)).GetProperty("accessToken").GetString()!, ValorDoCookie(linha!));
        }

        private Task<HttpResponseMessage> RefreshAsync(string? cookie)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
            if (cookie is not null)
                pedido.Headers.Add("Cookie", $"{NomeCookie}={cookie}");
            return _api.Cliente.SendAsync(pedido);
        }

        private Task<HttpResponseMessage> LogoutAsync(string? accessToken, string? cookie)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
            if (accessToken is not null)
                pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (cookie is not null)
                pedido.Headers.Add("Cookie", $"{NomeCookie}={cookie}");
            return _api.Cliente.SendAsync(pedido);
        }

        // ---------- Cookie do login ----------

        [Fact]
        public async Task Login_grava_o_refresh_token_em_cookie_httponly_secure_e_samesite_strict()
        {
            var r = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email = "admin@brassario.com", senha = "Admin@2026" }));

            var linha = LinhaDoCookie(r);
            Assert.NotNull(linha);
            Assert.Contains("httponly", linha!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", linha, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", linha, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("expires=", linha, StringComparison.OrdinalIgnoreCase);   // cookie persistente (30 dias)
        }

        [Fact]
        public async Task O_refresh_token_nao_aparece_no_corpo_da_resposta()
        {
            var r = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email = "admin@brassario.com", senha = "Admin@2026" }));

            var corpo = await r.Content.ReadAsStringAsync();
            var cookie = ValorDoCookie(LinhaDoCookie(r)!);
            Assert.DoesNotContain(cookie, corpo);
            Assert.DoesNotContain("refresh", corpo, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Login_com_senha_errada_nao_grava_cookie()
        {
            var r = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email = "admin@brassario.com", senha = "errada" }));

            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            Assert.Null(LinhaDoCookie(r));
        }

        // ---------- Refresh e rotação ----------

        [Fact]
        public async Task Refresh_com_cookie_valido_devolve_200_com_novo_token_e_novo_cookie()
        {
            var (_, cookie) = await EntrarAsync();

            var r = await RefreshAsync(cookie);

            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var corpo = await Ler(r);
            Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("accessToken").GetString()));
            Assert.Equal("Bearer", corpo.GetProperty("tokenType").GetString());
            Assert.Equal("ADMIN", corpo.GetProperty("usuario").GetProperty("perfil").GetString());

            var novaLinha = LinhaDoCookie(r);
            Assert.NotNull(novaLinha);
            Assert.NotEqual(cookie, ValorDoCookie(novaLinha!));   // rotação: o cookie mudou
            Assert.Contains("httponly", novaLinha!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", novaLinha, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task O_access_token_renovado_abre_as_rotas_protegidas()
        {
            var (_, cookie) = await EntrarAsync();

            var r = await RefreshAsync(cookie);
            var novoToken = (await Ler(r)).GetProperty("accessToken").GetString()!;

            var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", novoToken);
            var me = await _api.Cliente.SendAsync(pedido);

            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        [Fact]
        public async Task Refresh_nao_exige_o_cabecalho_authorization()
        {
            var (_, cookie) = await EntrarAsync();

            // o access token pode já ter vencido: o refresh é público e vale só pelo cookie
            var r = await RefreshAsync(cookie);

            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        }

        [Fact]
        public async Task O_cookie_antigo_nao_serve_depois_da_rotacao()
        {
            var (_, cookieAntigo) = await EntrarAsync();
            var primeira = await RefreshAsync(cookieAntigo);
            Assert.Equal(HttpStatusCode.OK, primeira.StatusCode);

            var reuso = await RefreshAsync(cookieAntigo);

            Assert.Equal(HttpStatusCode.Unauthorized, reuso.StatusCode);
            Assert.Equal("NAO_AUTENTICADO", (await Ler(reuso)).GetProperty("codigo").GetString());
        }

        [Fact]
        public async Task O_cookie_novo_da_rotacao_funciona()
        {
            var (_, cookie1) = await EntrarAsync();
            var r1 = await RefreshAsync(cookie1);
            var cookie2 = ValorDoCookie(LinhaDoCookie(r1)!);

            var r2 = await RefreshAsync(cookie2);

            Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        }

        [Fact]
        public async Task Refresh_sem_cookie_retorna_401_nao_autenticado()
        {
            var r = await RefreshAsync(null);

            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
            Assert.Equal("NAO_AUTENTICADO", (await Ler(r)).GetProperty("codigo").GetString());
        }

        [Fact]
        public async Task Refresh_com_cookie_inventado_retorna_401()
        {
            var r = await RefreshAsync("valor-inventado-que-nao-existe-no-banco");

            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        // ---------- Logout ----------

        [Fact]
        public async Task Logout_retorna_204_e_apaga_o_cookie()
        {
            var (token, cookie) = await EntrarAsync();

            var r = await LogoutAsync(token, cookie);

            Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
            var linha = LinhaDoCookie(r);
            Assert.NotNull(linha);
            Assert.Equal(string.Empty, ValorDoCookie(linha!));   // cookie esvaziado e vencido
        }

        [Fact]
        public async Task Logout_revoga_o_refresh_token_no_servidor()
        {
            var (token, cookie) = await EntrarAsync();

            await LogoutAsync(token, cookie);
            var r = await RefreshAsync(cookie);   // alguém que tenha copiado o cookie não renova mais

            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        [Fact]
        public async Task Logout_sem_access_token_retorna_401()
        {
            var (_, cookie) = await EntrarAsync();

            var r = await LogoutAsync(null, cookie);

            Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        }

        [Fact]
        public async Task Logout_de_um_aparelho_nao_derruba_o_outro()
        {
            var (tokenTablet, cookieTablet) = await EntrarAsync();
            var (_, cookieCelular) = await EntrarAsync();

            await LogoutAsync(tokenTablet, cookieTablet);

            Assert.Equal(HttpStatusCode.Unauthorized, (await RefreshAsync(cookieTablet)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(cookieCelular)).StatusCode);
        }

        [Fact]
        public async Task Logout_com_o_cookie_de_outro_usuario_nao_o_revoga()
        {
            var (tokenOperador, _) = await EntrarAsync("operador@brassario.com", "Operador@2026");
            var (_, cookieAdmin) = await EntrarAsync();

            await LogoutAsync(tokenOperador, cookieAdmin);   // operador logado tentando revogar o cookie do admin

            Assert.Equal(HttpStatusCode.OK, (await RefreshAsync(cookieAdmin)).StatusCode);
        }
    }
}
