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

        // Relógio que os testes podem avançar (para vencer o refresh token)
        private class RelogioMutavel : IRelogio
        {
            public DateTime Agora { get; set; } = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
            public DateOnly Hoje => DateOnly.FromDateTime(Agora);
            public DateTime AgoraUtc => Agora;
        }

        private static (AuthService Servico, AppDbContext Db, RelogioMutavel Relogio) Criar()
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
                ExpiresMinutes = 30,
                RefreshDays = 30
            });
            var relogio = new RelogioMutavel();

            return (new AuthService(db, Hasher, new JwtService(jwtOpcoes), jwtOpcoes, relogio), db, relogio);
        }

        private static Task<SessaoEmitida?> Entrar(AuthService servico, string email = "admin@brassario.com", string senha = SenhaAdmin) =>
            servico.IniciarSessaoAsync(new LoginRequest { Email = email, Senha = senha });

        // ---------- CT-01: login válido ----------

        [Fact]
        public async Task CT01_login_valido_devolve_token_perfil_e_validade()
        {
            var (servico, _, _) = Criar();

            var sessao = await Entrar(servico);

            Assert.NotNull(sessao);
            var r = sessao!.Resposta;
            Assert.False(string.IsNullOrWhiteSpace(r.AccessToken));
            Assert.Equal("Bearer", r.TokenType);
            Assert.Equal(30 * 60, r.ExpiresIn);
            Assert.Equal(PerfilUsuario.ADMIN, r.Usuario.Perfil);
            Assert.Equal("admin@brassario.com", r.Usuario.Email);
        }

        [Fact]
        public async Task CT01_o_token_traz_o_perfil_e_o_id_do_usuario()
        {
            var (servico, db, _) = Criar();

            var sessao = await Entrar(servico, "operador@brassario.com", "Operador@2026");

            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(sessao!.Resposta.AccessToken);
            var id = db.Usuarios.Single(u => u.Email == "operador@brassario.com").Id;
            Assert.Equal("OPERADOR", jwt.GetClaim("role").Value);
            Assert.Equal(id.ToString(), jwt.GetClaim("sub").Value);
        }

        [Theory]
        [InlineData("ADMIN@BRASSARIO.COM")]
        [InlineData("  admin@brassario.com  ")]
        public async Task CT01_o_email_nao_diferencia_maiusculas_nem_espacos(string email)
        {
            var (servico, _, _) = Criar();

            Assert.NotNull(await Entrar(servico, email));
        }

        [Fact]
        public async Task CT01_a_resposta_nao_expoe_senha_hash_nem_refresh_token()
        {
            var (servico, _, _) = Criar();

            var sessao = await Entrar(servico);

            var json = System.Text.Json.JsonSerializer.Serialize(sessao!.Resposta);
            Assert.DoesNotContain("$2", json);                       // prefixo de todo hash BCrypt
            Assert.DoesNotContain("senha", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(sessao.RefreshToken, json);        // o refresh token vai só no cookie
        }

        // ---------- CT-02: credenciais inválidas ----------

        [Fact]
        public async Task CT02_senha_errada_nao_devolve_token()
        {
            var (servico, _, _) = Criar();

            Assert.Null(await Entrar(servico, senha: "senha-errada"));
        }

        [Fact]
        public async Task CT02_email_inexistente_tem_o_mesmo_resultado_da_senha_errada()
        {
            var (servico, _, _) = Criar();

            var senhaErrada = await Entrar(servico, "admin@brassario.com", "senha-errada");
            var emailInexistente = await Entrar(servico, "ninguem@brassario.com", SenhaAdmin);

            Assert.Null(senhaErrada);
            Assert.Null(emailInexistente);   // nada revela se o e-mail existe
        }

        [Fact]
        public async Task CT02_usuario_inativo_nao_entra_mesmo_com_a_senha_certa()
        {
            var (servico, _, _) = Criar();

            Assert.Null(await Entrar(servico, "antigo@brassario.com", "Antigo@2026"));
        }

        [Fact]
        public async Task CT02_a_senha_de_um_usuario_nao_serve_para_outro()
        {
            var (servico, _, _) = Criar();

            Assert.Null(await Entrar(servico, "operador@brassario.com", SenhaAdmin));
        }

        [Fact]
        public async Task Login_invalido_nao_cria_refresh_token()
        {
            var (servico, db, _) = Criar();

            await Entrar(servico, senha: "senha-errada");

            Assert.Empty(db.RefreshTokens);
        }

        // ---------- AUTH-4: refresh token guardado como hash ----------

        [Fact]
        public async Task Login_cria_refresh_token_de_30_dias_guardado_so_como_hash()
        {
            var (servico, db, relogio) = Criar();

            var sessao = await Entrar(servico);

            var salvo = Assert.Single(db.RefreshTokens);
            Assert.NotEqual(sessao!.RefreshToken, salvo.TokenHash);                       // o token em si nunca é gravado
            Assert.Equal(RefreshTokenHelper.Hash(sessao.RefreshToken), salvo.TokenHash);
            Assert.DoesNotContain(sessao.RefreshToken, salvo.TokenHash);
            Assert.Equal(relogio.Agora.AddDays(30), salvo.ExpiraEm);
            Assert.False(salvo.Revogado);
            Assert.Equal(sessao.RefreshExpiraEm, salvo.ExpiraEm);
        }

        [Fact]
        public void Refresh_token_e_aleatorio_longo_e_o_hash_tem_64_caracteres()
        {
            var a = RefreshTokenHelper.Gerar();
            var b = RefreshTokenHelper.Gerar();

            Assert.NotEqual(a, b);
            Assert.True(a.Length >= 60);
            Assert.Equal(64, RefreshTokenHelper.Hash(a).Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", a);   // seguro para cookie, sem + / =
        }

        // ---------- AUTH-4: rotação a cada uso ----------

        [Fact]
        public async Task Refresh_valido_devolve_novo_access_token_e_novo_refresh_token()
        {
            var (servico, _, _) = Criar();
            var login = await Entrar(servico);

            var renovada = await servico.RenovarSessaoAsync(login!.RefreshToken);

            Assert.NotNull(renovada);
            Assert.False(string.IsNullOrWhiteSpace(renovada!.Resposta.AccessToken));
            Assert.Equal("admin@brassario.com", renovada.Resposta.Usuario.Email);
            Assert.Equal(PerfilUsuario.ADMIN, renovada.Resposta.Usuario.Perfil);
            Assert.NotEqual(login.RefreshToken, renovada.RefreshToken);
        }

        [Fact]
        public async Task Rotacao_revoga_o_token_usado_e_cria_um_novo()
        {
            var (servico, db, _) = Criar();
            var login = await Entrar(servico);

            var renovada = await servico.RenovarSessaoAsync(login!.RefreshToken);

            Assert.Equal(2, db.RefreshTokens.Count());
            Assert.True(db.RefreshTokens.Single(t => t.TokenHash == RefreshTokenHelper.Hash(login.RefreshToken)).Revogado);
            Assert.False(db.RefreshTokens.Single(t => t.TokenHash == RefreshTokenHelper.Hash(renovada!.RefreshToken)).Revogado);
        }

        [Fact]
        public async Task Refresh_token_so_pode_ser_usado_uma_vez()
        {
            var (servico, _, _) = Criar();
            var login = await Entrar(servico);

            var primeira = await servico.RenovarSessaoAsync(login!.RefreshToken);
            var reuso = await servico.RenovarSessaoAsync(login.RefreshToken);

            Assert.NotNull(primeira);
            Assert.Null(reuso);   // o antigo foi revogado na rotação
        }

        [Fact]
        public async Task O_novo_refresh_token_funciona_e_a_cadeia_continua()
        {
            var (servico, _, _) = Criar();
            var s1 = await Entrar(servico);
            var s2 = await servico.RenovarSessaoAsync(s1!.RefreshToken);
            var s3 = await servico.RenovarSessaoAsync(s2!.RefreshToken);

            Assert.NotNull(s3);
            Assert.Null(await servico.RenovarSessaoAsync(s2.RefreshToken));   // s2 já foi usado
        }

        [Fact]
        public async Task Cada_rotacao_renova_a_validade_por_mais_30_dias()
        {
            var (servico, _, relogio) = Criar();
            var login = await Entrar(servico);

            relogio.Agora = relogio.Agora.AddDays(20);
            var renovada = await servico.RenovarSessaoAsync(login!.RefreshToken);

            Assert.Equal(relogio.Agora.AddDays(30), renovada!.RefreshExpiraEm);
        }

        // ---------- AUTH-4: tokens inválidos ----------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("token-que-nunca-existiu")]
        public async Task Refresh_ausente_ou_desconhecido_e_recusado(string? token)
        {
            var (servico, _, _) = Criar();

            Assert.Null(await servico.RenovarSessaoAsync(token));
        }

        [Fact]
        public async Task Refresh_vencido_e_recusado()
        {
            var (servico, _, relogio) = Criar();
            var login = await Entrar(servico);

            relogio.Agora = relogio.Agora.AddDays(30).AddSeconds(1);

            Assert.Null(await servico.RenovarSessaoAsync(login!.RefreshToken));
        }

        [Fact]
        public async Task Refresh_ainda_dentro_da_validade_e_aceito()
        {
            var (servico, _, relogio) = Criar();
            var login = await Entrar(servico);

            relogio.Agora = relogio.Agora.AddDays(29);

            Assert.NotNull(await servico.RenovarSessaoAsync(login!.RefreshToken));
        }

        [Fact]
        public async Task Usuario_inativado_depois_do_login_nao_renova_a_sessao()
        {
            var (servico, db, _) = Criar();
            var login = await Entrar(servico, "operador@brassario.com", "Operador@2026");

            db.Usuarios.Single(u => u.Email == "operador@brassario.com").Ativo = false;
            await db.SaveChangesAsync();

            Assert.Null(await servico.RenovarSessaoAsync(login!.RefreshToken));
            Assert.True(db.RefreshTokens.All(t => t.Revogado));
        }

        [Fact]
        public async Task Refresh_de_um_usuario_devolve_a_sessao_dele_e_nao_de_outro()
        {
            var (servico, _, _) = Criar();
            var admin = await Entrar(servico);
            var operador = await Entrar(servico, "operador@brassario.com", "Operador@2026");

            var rAdmin = await servico.RenovarSessaoAsync(admin!.RefreshToken);
            var rOperador = await servico.RenovarSessaoAsync(operador!.RefreshToken);

            Assert.Equal(PerfilUsuario.ADMIN, rAdmin!.Resposta.Usuario.Perfil);
            Assert.Equal(PerfilUsuario.OPERADOR, rOperador!.Resposta.Usuario.Perfil);
        }

        [Fact]
        public async Task Login_apaga_os_refresh_tokens_vencidos_do_usuario()
        {
            var (servico, db, relogio) = Criar();
            await Entrar(servico);

            relogio.Agora = relogio.Agora.AddDays(31);
            await Entrar(servico);

            Assert.Single(db.RefreshTokens);   // o vencido foi limpo, só o novo ficou
        }

        // ---------- AUTH-4: logout ----------

        [Fact]
        public async Task Logout_revoga_o_refresh_token()
        {
            var (servico, db, _) = Criar();
            var login = await Entrar(servico);
            var usuarioId = db.Usuarios.Single(u => u.Email == "admin@brassario.com").Id;

            await servico.EncerrarSessaoAsync(usuarioId, login!.RefreshToken);

            Assert.True(db.RefreshTokens.Single().Revogado);
            Assert.Null(await servico.RenovarSessaoAsync(login.RefreshToken));   // depois do logout não renova mais
        }

        [Fact]
        public async Task Logout_nao_revoga_o_token_de_outro_usuario()
        {
            var (servico, db, _) = Criar();
            var login = await Entrar(servico);
            var outroUsuarioId = db.Usuarios.Single(u => u.Email == "operador@brassario.com").Id;

            await servico.EncerrarSessaoAsync(outroUsuarioId, login!.RefreshToken);

            Assert.False(db.RefreshTokens.Single().Revogado);
            Assert.NotNull(await servico.RenovarSessaoAsync(login.RefreshToken));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("token-que-nunca-existiu")]
        public async Task Logout_sem_token_ou_com_token_desconhecido_nao_falha(string? token)
        {
            var (servico, db, _) = Criar();
            var usuarioId = db.Usuarios.Single(u => u.Email == "admin@brassario.com").Id;

            await servico.EncerrarSessaoAsync(usuarioId, token);   // não lança exceção
        }

        [Fact]
        public async Task Logout_so_encerra_a_sessao_atual_e_nao_as_de_outros_aparelhos()
        {
            var (servico, db, _) = Criar();
            var tablet = await Entrar(servico);
            var celular = await Entrar(servico);
            var usuarioId = db.Usuarios.Single(u => u.Email == "admin@brassario.com").Id;

            await servico.EncerrarSessaoAsync(usuarioId, tablet!.RefreshToken);

            Assert.Null(await servico.RenovarSessaoAsync(tablet.RefreshToken));
            Assert.NotNull(await servico.RenovarSessaoAsync(celular!.RefreshToken));
        }
    }
}
