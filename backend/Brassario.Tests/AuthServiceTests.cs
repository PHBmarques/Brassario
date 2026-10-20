using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.Models;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace Brassario.Tests
{
    public class AuthServiceTests
    {
        private const string SenhaAdmin = "Admin@2026";

        private static readonly IPasswordHasher Hasher = new PasswordHasher();

        private static (AuthService Servico, AppDbContext Db) Criar()
        {
            var opcoesBanco = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new AppDbContext(opcoesBanco);

            db.Usuarios.AddRange(
                new Usuario { Nome = "Admin", Email = "admin@brassario.com", SenhaHash = Hasher.Hash(SenhaAdmin), Perfil = PerfilUsuario.ADMIN, Ativo = true },
                new Usuario { Nome = "Operador", Email = "operador@brassario.com", SenhaHash = Hasher.Hash("Operador@2026"), Perfil = PerfilUsuario.OPERADOR, Ativo = true },
                new Usuario { Nome = "Antigo", Email = "antigo@brassario.com", SenhaHash = Hasher.Hash("Antigo@2026"), Perfil = PerfilUsuario.OPERADOR, Ativo = false });
            db.SaveChanges();

            var jwtOpcoes = Options.Create(new JwtOptions
            {
                Key = "chave-de-teste-com-mais-de-32-caracteres-ok",
                Issuer = "brassario",
                ExpiresMinutes = 30
            });

            return (new AuthService(db, Hasher, new JwtService(jwtOpcoes), jwtOpcoes), db);
        }

        // ---------- CT-01: login válido ----------

        [Fact]
        public async Task CT01_login_valido_devolve_token_perfil_e_validade()
        {
            var (servico, _) = Criar();

            var r = await servico.LoginAsync(new LoginRequest { Email = "admin@brassario.com", Senha = SenhaAdmin });

            Assert.NotNull(r);
            Assert.False(string.IsNullOrWhiteSpace(r!.AccessToken));
            Assert.Equal("Bearer", r.TokenType);
            Assert.Equal(30 * 60, r.ExpiresIn);
            Assert.Equal(PerfilUsuario.ADMIN, r.Usuario.Perfil);
            Assert.Equal("admin@brassario.com", r.Usuario.Email);
        }

        [Fact]
        public async Task CT01_o_token_traz_o_perfil_e_o_id_do_usuario()
        {
            var (servico, db) = Criar();

            var r = await servico.LoginAsync(new LoginRequest { Email = "operador@brassario.com", Senha = "Operador@2026" });

            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(r!.AccessToken);
            var id = db.Usuarios.Single(u => u.Email == "operador@brassario.com").Id;
            Assert.Equal("OPERADOR", jwt.GetClaim("role").Value);
            Assert.Equal(id.ToString(), jwt.GetClaim("sub").Value);
        }

        [Theory]
        [InlineData("ADMIN@BRASSARIO.COM")]
        [InlineData("  admin@brassario.com  ")]
        public async Task CT01_o_email_nao_diferencia_maiusculas_nem_espacos(string email)
        {
            var (servico, _) = Criar();

            var r = await servico.LoginAsync(new LoginRequest { Email = email, Senha = SenhaAdmin });

            Assert.NotNull(r);
        }

        [Fact]
        public async Task CT01_a_resposta_nao_expoe_senha_nem_hash()
        {
            var (servico, _) = Criar();

            var r = await servico.LoginAsync(new LoginRequest { Email = "admin@brassario.com", Senha = SenhaAdmin });

            var json = System.Text.Json.JsonSerializer.Serialize(r);
            Assert.DoesNotContain("$2", json);                       // prefixo de todo hash BCrypt
            Assert.DoesNotContain("senha", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        }

        // ---------- CT-02: credenciais inválidas ----------

        [Fact]
        public async Task CT02_senha_errada_nao_devolve_token()
        {
            var (servico, _) = Criar();

            var r = await servico.LoginAsync(new LoginRequest { Email = "admin@brassario.com", Senha = "senha-errada" });

            Assert.Null(r);
        }

        [Fact]
        public async Task CT02_email_inexistente_tem_o_mesmo_resultado_da_senha_errada()
        {
            var (servico, _) = Criar();

            var senhaErrada = await servico.LoginAsync(new LoginRequest { Email = "admin@brassario.com", Senha = "senha-errada" });
            var emailInexistente = await servico.LoginAsync(new LoginRequest { Email = "ninguem@brassario.com", Senha = SenhaAdmin });

            Assert.Null(senhaErrada);
            Assert.Null(emailInexistente);   // nada revela se o e-mail existe
        }

        [Fact]
        public async Task CT02_usuario_inativo_nao_entra_mesmo_com_a_senha_certa()
        {
            var (servico, _) = Criar();

            var r = await servico.LoginAsync(new LoginRequest { Email = "antigo@brassario.com", Senha = "Antigo@2026" });

            Assert.Null(r);
        }

        [Fact]
        public async Task CT02_a_senha_de_um_usuario_nao_serve_para_outro()
        {
            var (servico, _) = Criar();

            var r = await servico.LoginAsync(new LoginRequest { Email = "operador@brassario.com", Senha = SenhaAdmin });

            Assert.Null(r);
        }
    }
}
