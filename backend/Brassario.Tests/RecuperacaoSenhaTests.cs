using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>Esqueci a senha, redefinir pelo link, alterar a própria senha, trocar o e-mail de recuperação e senha temporária do ADMIN.</summary>
    public class RecuperacaoSenhaTests
    {
        private const string SenhaCarlos = "Carlos@2026";
        private static readonly IPasswordHasher Hasher = new PasswordHasher();

        private class RelogioFixo : IRelogio
        {
            public DateTime Agora { get; set; } = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
            public DateOnly Hoje => DateOnly.FromDateTime(Agora);
            public DateTime AgoraUtc => Agora;
        }

        private record Cenario(AuthService Auth, UsuarioService Usuarios, AppDbContext Db, EmailFalso Email, RelogioFixo Relogio, Usuario Carlos, Usuario Admin);

        private static Cenario Criar(bool emailConfirmado = true)
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var relogio = new RelogioFixo();
            var admin = new Usuario { Nome = "Admin", Email = "admin@brassario.com", SenhaHash = Hasher.Hash("Admin@2026"), Perfil = PerfilUsuario.ADMIN };
            var carlos = new Usuario
            {
                Nome = "Carlos Lima",
                Email = "carlos@cervejaria.local",
                SenhaHash = Hasher.Hash(SenhaCarlos),
                Perfil = PerfilUsuario.OPERADOR,
                EmailRecuperacao = "carlos.lima@gmail.com",
                EmailRecuperacaoConfirmadoEm = emailConfirmado ? relogio.Agora.AddDays(-1) : null
            };
            db.Usuarios.AddRange(admin, carlos);
            db.SaveChanges();

            var jwt = Options.Create(new JwtOptions { Key = "chave-de-teste-com-mais-de-32-caracteres-ok", Issuer = "brassario", ExpiresMinutes = 30, RefreshDays = 30 });
            var email = new EmailFalso();
            var auth = new AuthService(db, Hasher, new JwtService(jwt), jwt, relogio, email, Options.Create(new AppOptions { UrlFront = "http://front" }));
            var usuarios = new UsuarioService(new UsuarioRepository(db), Hasher);
            return new Cenario(auth, usuarios, db, email, relogio, carlos, admin);
        }

        // O token só aparece no link do e-mail
        private static string TokenDoLink(string corpo, string caminho)
        {
            var inicio = corpo.IndexOf($"/{caminho}?token=", StringComparison.Ordinal) + caminho.Length + 8;
            var fim = corpo.IndexOf('"', inicio);
            return Uri.UnescapeDataString(corpo[inicio..fim]);
        }

        // ---------- Esqueci a senha ----------

        [Theory]
        [InlineData("carlos@cervejaria.local")]
        [InlineData("CARLOS.LIMA@gmail.com ")]
        public async Task Esqueci_senha_envia_o_link_para_o_email_de_recuperacao(string informado)
        {
            var c = Criar();

            await c.Auth.SolicitarRedefinicaoSenhaAsync(informado);

            var enviado = Assert.Single(c.Email.Enviados);
            Assert.Equal("carlos.lima@gmail.com", enviado.Para);
            Assert.Contains("http://front/redefinir-senha?token=", enviado.Corpo);
        }

        [Fact]
        public async Task Esqueci_senha_nao_envia_nada_sem_email_confirmado_ou_conta_inexistente()
        {
            var c = Criar(emailConfirmado: false);

            await c.Auth.SolicitarRedefinicaoSenhaAsync("carlos@cervejaria.local");
            await c.Auth.SolicitarRedefinicaoSenhaAsync("ninguem@brassario.com");

            Assert.Empty(c.Email.Enviados);
        }

        [Fact]
        public async Task Redefinir_pelo_link_troca_a_senha_e_o_link_nao_vale_de_novo()
        {
            var c = Criar();
            await c.Auth.SolicitarRedefinicaoSenhaAsync("carlos@cervejaria.local");
            var token = TokenDoLink(c.Email.Enviados[0].Corpo, "redefinir-senha");

            var ok = await c.Auth.RedefinirSenhaAsync(new RedefinirSenhaRequest { Token = token, NovaSenha = "NovaSenha@1", ConfirmarSenha = "NovaSenha@1" });

            Assert.True(ok);
            Assert.True(Hasher.Verify("NovaSenha@1", c.Db.Usuarios.Single(u => u.Id == c.Carlos.Id).SenhaHash));
            Assert.False(await c.Auth.RedefinirSenhaAsync(new RedefinirSenhaRequest { Token = token, NovaSenha = "OutraSenha@1", ConfirmarSenha = "OutraSenha@1" }));
        }

        [Fact]
        public async Task Redefinir_com_link_vencido_falha()
        {
            var c = Criar();
            await c.Auth.SolicitarRedefinicaoSenhaAsync("carlos@cervejaria.local");
            var token = TokenDoLink(c.Email.Enviados[0].Corpo, "redefinir-senha");
            c.Relogio.Agora = c.Relogio.Agora.AddHours(2);

            Assert.False(await c.Auth.RedefinirSenhaAsync(new RedefinirSenhaRequest { Token = token, NovaSenha = "NovaSenha@1", ConfirmarSenha = "NovaSenha@1" }));
        }

        [Fact]
        public async Task Redefinir_valida_a_senha_e_a_confirmacao()
        {
            var c = Criar();
            await c.Auth.SolicitarRedefinicaoSenhaAsync("carlos@cervejaria.local");
            var token = TokenDoLink(c.Email.Enviados[0].Corpo, "redefinir-senha");

            var erro = await Assert.ThrowsAsync<ValidacaoException>(() =>
                c.Auth.RedefinirSenhaAsync(new RedefinirSenhaRequest { Token = token, NovaSenha = "curta", ConfirmarSenha = "outra" }));

            Assert.Contains("novaSenha", erro.Detalhes.Keys);
            Assert.Contains("confirmarSenha", erro.Detalhes.Keys);
        }

        // ---------- Alterar minha senha ----------

        [Fact]
        public async Task Alterar_senha_confere_a_senha_atual()
        {
            var c = Criar();

            var erro = await Assert.ThrowsAsync<ValidacaoException>(() =>
                c.Auth.AlterarSenhaAsync(c.Carlos.Id, new AlterarSenhaRequest { SenhaAtual = "errada", NovaSenha = "NovaSenha@1", ConfirmarSenha = "NovaSenha@1" }));

            Assert.Contains("senhaAtual", erro.Detalhes.Keys);
        }

        [Fact]
        public async Task Alterar_senha_troca_e_abre_sessao_nova()
        {
            var c = Criar();

            var sessao = await c.Auth.AlterarSenhaAsync(c.Carlos.Id,
                new AlterarSenhaRequest { SenhaAtual = SenhaCarlos, NovaSenha = "NovaSenha@1", ConfirmarSenha = "NovaSenha@1" });

            Assert.NotNull(sessao);
            Assert.True(Hasher.Verify("NovaSenha@1", c.Db.Usuarios.Single(u => u.Id == c.Carlos.Id).SenhaHash));
        }

        [Fact]
        public async Task Alterar_senha_nao_aceita_a_mesma_senha()
        {
            var c = Criar();

            var erro = await Assert.ThrowsAsync<ValidacaoException>(() =>
                c.Auth.AlterarSenhaAsync(c.Carlos.Id, new AlterarSenhaRequest { SenhaAtual = SenhaCarlos, NovaSenha = SenhaCarlos, ConfirmarSenha = SenhaCarlos }));

            Assert.Contains("novaSenha", erro.Detalhes.Keys);
        }

        // ---------- E-mail de recuperação ----------

        [Fact]
        public async Task Trocar_email_de_recuperacao_fica_pendente_e_envia_confirmacao()
        {
            var c = Criar();

            var usuario = await c.Auth.AlterarEmailRecuperacaoAsync(c.Carlos.Id, new EmailRecuperacaoRequest { Email = "Carlos.Novo@gmail.com" });

            Assert.Equal("carlos.novo@gmail.com", usuario!.EmailRecuperacao);
            Assert.False(usuario.EmailRecuperacaoConfirmado);
            var enviado = Assert.Single(c.Email.Enviados);
            Assert.Equal("carlos.novo@gmail.com", enviado.Para);

            Assert.True(await c.Auth.ConfirmarEmailAsync(TokenDoLink(enviado.Corpo, "confirmar-email")));
        }

        [Fact]
        public async Task Email_de_recuperacao_nao_pode_ser_o_de_login()
        {
            var c = Criar();

            await Assert.ThrowsAsync<ValidacaoException>(() =>
                c.Auth.AlterarEmailRecuperacaoAsync(c.Carlos.Id, new EmailRecuperacaoRequest { Email = "carlos@cervejaria.local" }));
        }

        // ---------- Senha temporária gerada pelo ADMIN ----------

        [Fact]
        public async Task Admin_gera_senha_temporaria_e_exige_troca_no_proximo_acesso()
        {
            var c = Criar();

            var r = await c.Usuarios.GerarSenhaTemporariaAsync(c.Carlos.Id, c.Admin.Id);

            Assert.Matches("^[A-Za-z2-9]{4}-[A-Za-z2-9]{4}$", r.SenhaTemporaria);
            Assert.True(r.Usuario.DeveTrocarSenha);
            Assert.True(Hasher.Verify(r.SenhaTemporaria, c.Db.Usuarios.Single(u => u.Id == c.Carlos.Id).SenhaHash));
        }

        [Fact]
        public async Task Admin_nao_gera_senha_temporaria_para_si_mesmo()
        {
            var c = Criar();

            await Assert.ThrowsAsync<NegocioException>(() => c.Usuarios.GerarSenhaTemporariaAsync(c.Admin.Id, c.Admin.Id));
        }
    }
}
