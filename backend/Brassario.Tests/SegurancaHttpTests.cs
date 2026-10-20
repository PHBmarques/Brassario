using Brassario.Api.Controllers;
using Brassario.Api.Data;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;
namespace Brassario.Tests
{
    public class SegurancaHttpTests : IClassFixture<SegurancaHttpTests.ApiFalsa>
    {
        private const string SenhaAdmin = "Admin@2026";
        private const string SenhaOperador = "Operador@2026";

        private readonly ApiFalsa _api;

        public SegurancaHttpTests(ApiFalsa api) => _api = api;

        // ---------- Servidor de teste ----------

        public class ApiFalsa : IAsyncLifetime
        {
            private WebApplication _app = null!;
            public HttpClient Cliente { get; private set; } = null!;

            public async Task InitializeAsync()
            {
                var jwt = new JwtOptions { Key = "chave-de-teste-com-mais-de-32-caracteres-ok", Issuer = "brassario", ExpiresMinutes = 30 };

                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseUrls("http://127.0.0.1:0");   // porta livre
                builder.Logging.ClearProviders();

                builder.Services.AddControllers()
                    .AddApplicationPart(typeof(AuthController).Assembly)
                    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));   // enums como texto, igual ao Program.cs
                                                                                                                   // O nome é gerado UMA vez: se ficasse dentro da lambda, cada requisição veria um banco vazio diferente
                var nomeDoBanco = Guid.NewGuid().ToString();
                builder.Services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(nomeDoBanco));
                builder.Services.Configure<JwtOptions>(o => { o.Key = jwt.Key; o.Issuer = jwt.Issuer; o.ExpiresMinutes = jwt.ExpiresMinutes; });
                builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
                builder.Services.AddScoped<IJwtService, JwtService>();
                builder.Services.AddScoped<IAuthService, AuthService>();
                builder.Services.AddScoped<IInsumoService, InsumoServiceFalso>();
                builder.Services.AddScoped<IArmarioService, ArmarioServiceFalso>();

                // Espelho do que está no Program.cs (AddJwtBearer): mesmas validações e mesmos erros 401 e 403
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(o =>
                    {
                        o.MapInboundClaims = false;
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
                            RoleClaimType = "role"
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

                _app = builder.Build();
                _app.UseAuthentication();
                _app.UseAuthorization();
                _app.MapControllers();

                // Usuários de teste
                using (var escopo = _app.Services.CreateScope())
                {
                    var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
                    var hasher = escopo.ServiceProvider.GetRequiredService<IPasswordHasher>();
                    db.Usuarios.AddRange(
                        new Usuario { Nome = "Admin", Email = "admin@brassario.com", SenhaHash = hasher.Hash(SenhaAdmin), Perfil = PerfilUsuario.ADMIN },
                        new Usuario { Nome = "Operador", Email = "operador@brassario.com", SenhaHash = hasher.Hash(SenhaOperador), Perfil = PerfilUsuario.OPERADOR });
                    await db.SaveChangesAsync();
                }

                await _app.StartAsync();

                var endereco = _app.Services.GetRequiredService<IServer>()
                    .Features.Get<IServerAddressesFeature>()!.Addresses.First();
                Cliente = new HttpClient { BaseAddress = new Uri(endereco) };
            }

            public async Task DisposeAsync()
            {
                Cliente.Dispose();
                await _app.StopAsync();
                await _app.DisposeAsync();
            }
        }

        // Serviços falsos: só precisam existir para o controller ser criado
        private class InsumoServiceFalso : IInsumoService
        {
            public Task<List<InsumoResponse>> ListarAsync(string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default) =>
                Task.FromResult(new List<InsumoResponse>());
            public Task<InsumoResponse> ObterAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<InsumoResponse> CriarAsync(Brassario.Api.DTOs.Request.InsumoRequest request, CancellationToken ct = default) =>
                Task.FromResult(new InsumoResponse(1, request.Nome, CategoriaInsumo.MALTE, UnidadeMedida.KG, 0, 15, 45, true));
            public Task<InsumoResponse> AtualizarAsync(long id, Brassario.Api.DTOs.Request.InsumoRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<InsumoResponse> AlterarStatusAsync(long id, bool ativo, CancellationToken ct = default) => throw new NotSupportedException();
        }

        private class ArmarioServiceFalso : IArmarioService
        {
            public Task<List<ArmarioItemResponse>> ListarAsync(string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default) =>
                Task.FromResult(new List<ArmarioItemResponse>());
            public Task<ArmarioDetalheResponse> ObterDetalheAsync(long insumoId, bool incluirEsgotados, CancellationToken ct = default) =>
                throw new NotSupportedException();
        }

        // ---------- Apoio ----------

        private static StringContent Json(object corpo) =>
            new(JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json");

        private static async Task<JsonElement> Ler(HttpResponseMessage resposta) =>
            JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement;

        private async Task<string> TokenAsync(string email, string senha)
        {
            var resposta = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email, senha }));
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            return (await Ler(resposta)).GetProperty("accessToken").GetString()!;
        }

        private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string rota, string? token = null, object? corpo = null)
        {
            var pedido = new HttpRequestMessage(metodo, rota);
            if (token is not null)
                pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (corpo is not null)
                pedido.Content = Json(corpo);
            return await _api.Cliente.SendAsync(pedido);
        }

        private static readonly object NovoInsumo = new { nome = "Malte Pilsen", categoria = "MALTE", unidadeMedida = "KG", estoqueMinimo = 10 };

        // ---------- CT-01: login válido retorna token ----------

        [Fact]
        public async Task CT01_login_valido_retorna_200_com_token_e_perfil()
        {
            var resposta = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email = "admin@brassario.com", senha = SenhaAdmin }));

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
            var corpo = await Ler(resposta);
            Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("accessToken").GetString()));
            Assert.Equal("Bearer", corpo.GetProperty("tokenType").GetString());
            Assert.Equal("ADMIN", corpo.GetProperty("usuario").GetProperty("perfil").GetString());
            Assert.DoesNotContain("senha", await resposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CT01_o_token_do_login_abre_as_rotas_protegidas()
        {
            var token = await TokenAsync("admin@brassario.com", SenhaAdmin);

            var me = await EnviarAsync(HttpMethod.Get, "/api/v1/auth/me", token);
            var armario = await EnviarAsync(HttpMethod.Get, "/api/v1/armario", token);

            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            Assert.Equal("admin@brassario.com", (await Ler(me)).GetProperty("email").GetString());
            Assert.Equal(HttpStatusCode.OK, armario.StatusCode);
        }

        // ---------- CT-02: senha errada retorna 401 ----------

        [Fact]
        public async Task CT02_senha_errada_retorna_401_credenciais_invalidas()
        {
            var resposta = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email = "admin@brassario.com", senha = "errada" }));

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
            var corpo = await Ler(resposta);
            Assert.Equal("CREDENCIAIS_INVALIDAS", corpo.GetProperty("codigo").GetString());
            Assert.False(corpo.TryGetProperty("accessToken", out _));
        }

        [Fact]
        public async Task CT02_email_inexistente_responde_igual_a_senha_errada()
        {
            var senhaErrada = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email = "admin@brassario.com", senha = "errada" }));
            var inexistente = await _api.Cliente.PostAsync("/api/v1/auth/login", Json(new { email = "ninguem@brassario.com", senha = "errada" }));

            Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
            Assert.Equal(await senhaErrada.Content.ReadAsStringAsync(), await inexistente.Content.ReadAsStringAsync());
        }

        // ---------- CT-04: acesso sem token retorna 401 ----------

        [Fact]
        public async Task CT04_armario_sem_token_retorna_401_nao_autenticado()
        {
            var resposta = await EnviarAsync(HttpMethod.Get, "/api/v1/armario");

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
            Assert.Equal("NAO_AUTENTICADO", (await Ler(resposta)).GetProperty("codigo").GetString());
        }

        [Theory]
        [InlineData("GET", "/api/v1/armario")]
        [InlineData("GET", "/api/v1/insumos")]
        [InlineData("POST", "/api/v1/insumos")]
        [InlineData("GET", "/api/v1/receitas")]
        [InlineData("GET", "/api/v1/fornecedores")]
        [InlineData("POST", "/api/v1/bateladas")]
        [InlineData("POST", "/api/v1/lotes")]
        [InlineData("GET", "/api/v1/auth/me")]
        public async Task CT04_nenhuma_rota_protegida_abre_sem_token(string metodo, string rota)
        {
            var resposta = await EnviarAsync(new HttpMethod(metodo), rota, corpo: metodo == "POST" ? NovoInsumo : null);

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        [Fact]
        public async Task CT04_token_invalido_retorna_401()
        {
            var resposta = await EnviarAsync(HttpMethod.Get, "/api/v1/armario", "isto.nao.e-um-token");

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        [Fact]
        public async Task CT04_token_adulterado_retorna_401()
        {
            var token = await TokenAsync("operador@brassario.com", SenhaOperador);
            var adulterado = token[..^3] + (token.EndsWith("AAA") ? "BBB" : "AAA");   // quebra a assinatura

            var resposta = await EnviarAsync(HttpMethod.Get, "/api/v1/armario", adulterado);

            Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        }

        // ---------- CT-05: operador em rota de ADMIN retorna 403 ----------

        [Fact]
        public async Task CT05_operador_em_post_insumos_retorna_403_acesso_negado()
        {
            var token = await TokenAsync("operador@brassario.com", SenhaOperador);

            var resposta = await EnviarAsync(HttpMethod.Post, "/api/v1/insumos", token, NovoInsumo);

            Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
            Assert.Equal("ACESSO_NEGADO", (await Ler(resposta)).GetProperty("codigo").GetString());
        }

        [Fact]
        public async Task CT05_admin_no_mesmo_endpoint_e_aceito()
        {
            var token = await TokenAsync("admin@brassario.com", SenhaAdmin);

            var resposta = await EnviarAsync(HttpMethod.Post, "/api/v1/insumos", token, NovoInsumo);

            Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        }

        [Theory]
        [InlineData("PUT", "/api/v1/insumos/1")]
        [InlineData("PATCH", "/api/v1/insumos/1/status")]
        [InlineData("POST", "/api/v1/receitas")]
        [InlineData("PUT", "/api/v1/receitas/1")]
        [InlineData("PATCH", "/api/v1/receitas/1/status")]
        [InlineData("POST", "/api/v1/fornecedores")]
        [InlineData("PUT", "/api/v1/fornecedores/1")]
        public async Task CT05_operador_nas_demais_rotas_de_admin_retorna_403(string metodo, string rota)
        {
            var token = await TokenAsync("operador@brassario.com", SenhaOperador);

            var resposta = await EnviarAsync(new HttpMethod(metodo), rota, token, NovoInsumo);

            Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        }

        [Theory]
        [InlineData("/api/v1/insumos")]
        [InlineData("/api/v1/armario")]
        public async Task CT05_operador_continua_podendo_consultar(string rota)
        {
            var token = await TokenAsync("operador@brassario.com", SenhaOperador);

            var resposta = await EnviarAsync(HttpMethod.Get, rota, token);

            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }
    }
}
