using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>CT-03: bloqueio de login (5 senhas erradas seguidas bloqueiam o e-mail por 15 minutos).</summary>
    public class BloqueioDeLoginTests
    {
        private const string Senha = "Admin@2026";
        private static readonly IPasswordHasher Hasher = new PasswordHasher();

        private class RelogioMutavel : IRelogio
        {
            public DateTime Agora { get; set; } = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
            public DateOnly Hoje => DateOnly.FromDateTime(Agora);
            public DateTime AgoraUtc => Agora;
        }

        private static (AuthService Servico, RelogioMutavel Relogio) Criar()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            db.Usuarios.Add(new Usuario { Nome = "Admin", Email = "admin@brassario.com", SenhaHash = Hasher.Hash(Senha), Perfil = PerfilUsuario.ADMIN, Ativo = true });
            db.SaveChanges();

            var jwt = Options.Create(new JwtOptions { Key = "chave-de-teste-com-mais-de-32-caracteres-ok", Issuer = "brassario", ExpiresMinutes = 30, RefreshDays = 30 });
            var relogio = new RelogioMutavel();
            var bloqueio = new BloqueioDeLogin(relogio, Options.Create(new SegurancaOptions()));
            return (new AuthService(db, Hasher, new JwtService(jwt), jwt, relogio, new EmailFalso(), Options.Create(new AppOptions()), bloqueio), relogio);
        }

        private static Task<SessaoEmitida?> Entrar(AuthService s, string senha, string email = "admin@brassario.com") =>
            s.IniciarSessaoAsync(new LoginRequest { Email = email, Senha = senha });

        [Fact]
        public async Task CT03_cinco_senhas_erradas_bloqueiam_e_a_sexta_tentativa_devolve_LOGIN_BLOQUEADO()
        {
            var (servico, _) = Criar();

            for (var i = 1; i <= 5; i++)
                Assert.Null(await Entrar(servico, "errada"));   // 401 CREDENCIAIS_INVALIDAS

            var ex = await Assert.ThrowsAsync<LoginBloqueadoException>(() => Entrar(servico, "errada"));
            Assert.Equal(15, ex.MinutosRestantes);
        }

        [Fact]
        public async Task Bloqueado_nem_a_senha_correta_entra()
        {
            var (servico, _) = Criar();
            for (var i = 0; i < 5; i++) await Entrar(servico, "errada");

            await Assert.ThrowsAsync<LoginBloqueadoException>(() => Entrar(servico, Senha));
        }

        [Fact]
        public async Task Depois_de_15_minutos_o_login_e_liberado()
        {
            var (servico, relogio) = Criar();
            for (var i = 0; i < 5; i++) await Entrar(servico, "errada");

            relogio.Agora = relogio.Agora.AddMinutes(15).AddSeconds(1);

            Assert.NotNull(await Entrar(servico, Senha));
        }

        [Fact]
        public async Task Login_correto_zera_a_contagem_de_falhas()
        {
            var (servico, _) = Criar();
            for (var i = 0; i < 4; i++) await Entrar(servico, "errada");
            Assert.NotNull(await Entrar(servico, Senha));          // zera

            for (var i = 0; i < 4; i++) Assert.Null(await Entrar(servico, "errada"));
            Assert.NotNull(await Entrar(servico, Senha));          // ainda não bloqueou (só 4 seguidas)
        }

        [Fact]
        public async Task O_bloqueio_vale_por_e_mail_e_inclusive_para_e_mail_que_nao_existe()
        {
            var (servico, _) = Criar();
            for (var i = 0; i < 5; i++) Assert.Null(await Entrar(servico, "x", "ninguem@brassario.com"));

            await Assert.ThrowsAsync<LoginBloqueadoException>(() => Entrar(servico, "x", "ninguem@brassario.com"));
            Assert.NotNull(await Entrar(servico, Senha));          // outro e-mail não é afetado
        }

        [Fact]
        public async Task O_e_mail_nao_diferencia_maiusculas_na_contagem()
        {
            var (servico, _) = Criar();
            for (var i = 0; i < 5; i++) await Entrar(servico, "errada", i % 2 == 0 ? "ADMIN@brassario.com" : "admin@brassario.com");

            await Assert.ThrowsAsync<LoginBloqueadoException>(() => Entrar(servico, "errada"));
        }

        [Fact]
        public async Task Falhas_antigas_nao_somam_com_as_novas()
        {
            var (servico, relogio) = Criar();
            for (var i = 0; i < 4; i++) await Entrar(servico, "errada");

            relogio.Agora = relogio.Agora.AddMinutes(20);          // a contagem esfriou
            for (var i = 0; i < 4; i++) Assert.Null(await Entrar(servico, "errada"));
            Assert.NotNull(await Entrar(servico, Senha));
        }
    }
}
