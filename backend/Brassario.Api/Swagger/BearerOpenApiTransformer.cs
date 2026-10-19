using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Brassario.Api.Swagger
{
    public class BearerOpenApiTransformer : IOpenApiDocumentTransformer
    {
        public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
        {
            document.Info = new OpenApiInfo
            {
                Title = "Brassário API",
                Version = "v1",
                Description = "Controle de estoque e insumos para cervejarias artesanais. " +
                             "Faça login em POST /api/v1/auth/login, copie o accessToken e clique em Authorize."
            };

            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Cole só o token (sem a palavra Bearer). Ele vale 30 minutos."
                }
            };

            // Exige o esquema em todas as rotas; as rotas com [AllowAnonymous] simplesmente ignoram o token
            document.Security =
            [
                new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
            }
            ];

            return Task.CompletedTask;
        }
    }
}
