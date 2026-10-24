using Brassario.Api.Data;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Middleware;
using Brassario.Api.Repositories;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Brassario.Api.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(o =>
        o.InvalidModelStateResponseFactory = ValidacaoResposta.Criar);
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerOpenApiTransformer>());

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();

var jwtSecao = builder.Configuration.GetSection(JwtOptions.Secao);
builder.Services.Configure<JwtOptions>(jwtSecao);
var jwt = jwtSecao.Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwt.Key) || jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key ausente ou com menos de 32 caracteres. Defina JWT_KEY no .env.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // mantém os nomes curtos: sub, email, role
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Issuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role" // faz [Authorize(Roles = "ADMIN")] funcionar
        };
        o.Events = new JwtBearerEvents
        {
            OnChallenge = async contexto =>
            {
                contexto.HandleResponse();
                contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await contexto.Response.WriteAsJsonAsync(new ErroResponse(
                    401, "NAO_AUTENTICADO", "Autenticação necessária ou token inválido/expirado."));
            },
            OnForbidden = async contexto =>
            {
                contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
                await contexto.Response.WriteAsJsonAsync(new ErroResponse(
                    403, "ACESSO_NEGADO", "Você não tem permissão para realizar esta ação."));
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IInsumoRepository, InsumoRepository>();
builder.Services.AddScoped<IInsumoService, InsumoService>();
builder.Services.AddSingleton<IRelogio, Relogio>();
builder.Services.AddScoped<IFornecedorRepository, FornecedorRepository>();
builder.Services.AddScoped<ILoteRepository, LoteRepository>();
builder.Services.AddScoped<ILoteService, LoteService>();
builder.Services.AddScoped<IEntradaService, EntradaService>();
builder.Services.AddScoped<IArmarioService, ArmarioService>();
builder.Services.AddScoped<IAlertasService, AlertasService>();
builder.Services.AddScoped<IReceitaRepository, ReceitaRepository>();
builder.Services.AddScoped<IReceitaService, ReceitaService>();
builder.Services.AddScoped<IDisponibilidadeService, DisponibilidadeService>();
builder.Services.AddScoped<IPreviaProducaoService, PreviaProducaoService>();
builder.Services.AddScoped<IBateladaRepository, BateladaRepository>();
builder.Services.AddScoped<IBateladaService, BateladaService>();
builder.Services.AddScoped<IHistoricoRepository, HistoricoRepository>();
builder.Services.AddScoped<IHistoricoService, HistoricoService>();
builder.Services.AddScoped<IFornecedorCadastroRepository, FornecedorRepository>();
builder.Services.AddScoped<IFornecedorService, FornecedorService>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Secao));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Secao));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.Configure<SegurancaOptions>(builder.Configuration.GetSection(SegurancaOptions.Secao));
var seguranca = builder.Configuration.GetSection(SegurancaOptions.Secao).Get<SegurancaOptions>() ?? new SegurancaOptions();

builder.Services.AddSingleton<ILimitadorTentativas, LimitadorTentativas>();
builder.Services.AddScoped<IRecuperacaoSenhaService, RecuperacaoSenhaService>();

// Limite por IP do "esqueci minha senha". O limite por e-mail fica no serviço (precisa do corpo da requisição).
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy("esqueci-senha", contexto => RateLimitPartition.GetFixedWindowLimiter(
        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = seguranca.EsqueciSenhaPorHora,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0
        }));

    o.OnRejected = async (contexto, ct) =>
    {
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await contexto.HttpContext.Response.WriteAsJsonAsync(new ErroResponse(
            429, "MUITAS_TENTATIVAS", "Muitas tentativas. Aguarde um pouco e tente novamente."), ct);
    };
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();                  // cria as tabelas se faltarem
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    await DbSeeder.SeedAsync(db, hasher, app.Configuration, app.Logger);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();


// Configure the HTTP request pipeline.
if(app.Environment.IsDevelopment())
{
    app.MapOpenApi();                                   // documento em /openapi/v1.json
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/openapi/v1.json", "Brassário API v1");
        o.RoutePrefix = "swagger";                      // tela em /swagger
        o.DocumentTitle = "Brassário API";
    });
}

app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TrocaDeSenhaMiddleware>();


app.MapControllers();

app.Run();

