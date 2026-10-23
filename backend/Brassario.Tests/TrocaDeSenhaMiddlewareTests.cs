using System.Security.Claims;
using Brassario.Api.Middleware;
using Brassario.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Brassario.Tests
{
    public class TrocaDeSenhaMiddlewareTests
    {
        private static DefaultHttpContext Contexto(bool trocaPendente, params object[] metadados)
        {
            var claims = new List<Claim> { new("sub", "1"), new("role", "OPERADOR") };
            if (trocaPendente)
                claims.Add(new Claim("pwd_change", "1"));

            var contexto = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "teste"))
            };
            contexto.Response.Body = new MemoryStream();
            contexto.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
                new EndpointMetadataCollection(metadados), "teste"));
            return contexto;
        }

        private static async Task<(bool Passou, DefaultHttpContext Contexto)> Executar(DefaultHttpContext contexto)
        {
            var passou = false;
            var middleware = new TrocaDeSenhaMiddleware(_ => { passou = true; return Task.CompletedTask; });
            await middleware.InvokeAsync(contexto);
            return (passou, contexto);
        }

        private static string Corpo(DefaultHttpContext contexto)
        {
            contexto.Response.Body.Position = 0;
            return new StreamReader(contexto.Response.Body).ReadToEnd();
        }

        [Fact]
        public async Task Bloqueia_rota_autenticada_quando_a_troca_esta_pendente()
        {
            var (passou, ctx) = await Executar(Contexto(true, new AuthorizeAttribute()));

            Assert.False(passou);
            Assert.Equal(403, ctx.Response.StatusCode);
            Assert.Contains("TROCA_DE_SENHA_OBRIGATORIA", Corpo(ctx));
        }

        [Fact]
        public async Task Libera_rota_marcada_com_PermitirComTrocaPendente()
        {
            var (passou, ctx) = await Executar(Contexto(true, new AuthorizeAttribute(), new PermitirComTrocaPendenteAttribute()));

            Assert.True(passou);
            Assert.Equal(200, ctx.Response.StatusCode);
        }

        [Fact]
        public async Task Libera_rota_anonima_mesmo_com_token_de_troca_pendente()
        {
            var (passou, _) = await Executar(Contexto(true, new AllowAnonymousAttribute()));

            Assert.True(passou);
        }

        [Fact]
        public async Task Libera_rota_sem_autorizacao()
        {
            var (passou, _) = await Executar(Contexto(true));

            Assert.True(passou);
        }

        [Fact]
        public async Task Libera_conta_sem_troca_pendente()
        {
            var (passou, ctx) = await Executar(Contexto(false, new AuthorizeAttribute()));

            Assert.True(passou);
            Assert.Equal(200, ctx.Response.StatusCode);
        }
    }
}