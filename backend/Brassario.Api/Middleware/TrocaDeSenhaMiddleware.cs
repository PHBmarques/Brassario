using Brassario.Api.DTOs.Response;
using Brassario.Api.Security;
using Microsoft.AspNetCore.Authorization;

namespace Brassario.Api.Middleware
{
    /// <summary>
    /// Barra, na API, as contas com senha temporária: enquanto o token tiver a claim pwd_change,
    /// só as rotas marcadas com [PermitirComTrocaPendente] respondem. Rotas públicas não são afetadas.
    /// </summary>
    public class TrocaDeSenhaMiddleware
    {
        private readonly RequestDelegate _next;

        public TrocaDeSenhaMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            var endpoint = context.GetEndpoint();

            var rotaProtegida = endpoint is not null
                && endpoint.Metadata.GetMetadata<IAuthorizeData>() is not null
                && endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null;

            var liberada = endpoint?.Metadata.GetMetadata<PermitirComTrocaPendenteAttribute>() is not null;

            var trocaPendente = context.User.Identity?.IsAuthenticated == true
                && context.User.HasClaim(JwtService.ClaimTrocaSenha, "1");

            if (rotaProtegida && trocaPendente && !liberada)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ErroResponse(
                    403,
                    "TROCA_DE_SENHA_OBRIGATORIA",
                    "Você precisa trocar a senha temporária antes de continuar."));
                return;
            }

            await _next(context);
        }
    }
}