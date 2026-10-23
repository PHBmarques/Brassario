using System.Text.RegularExpressions;
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
    public class ContaServiceTests
    {
        private static readonly IPasswordHasher Hasher = new PasswordHasher();

        private class RelogioMutavel : IRelogio
        {
            public DateTime Agora { get; set; } = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
            public DateOnly Hoje => DateOnly.FromDateTime(Agora);
            public DateTime AgoraUtc => Agora;
        }

        private static (AuthService Servico, AppDbContext Db, EmailFalso Email, RelogioMutavel Relogio, long Id) Criar()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var relogio = new RelogioMutavel();
            var ana = new Usuario
            {
                Nome = "Ana",
                Email = "ana@brassario.local",
                SenhaHash = Hasher.Hash("SenhaAtual@1"),
                Perfil = PerfilUsuario.OPERADOR,
                Ativo = true,
                EmailRecuperacao = "antigo@gmail.com",
                EmailRecuperacaoConfirmadoEm = relogio.Agora
            };
            var inativo = new Usuario
            {
                Nome = "Inativo",
                Email = "inativo@brassario.local",
                SenhaHash = Hasher.Hash("SenhaAtual@1"),
                Ativo = false
            };
            db.Usuarios.AddRange(ana, inativo);
            db.SaveChanges();

            var jwt = Options.Create(new JwtOptions
            {
                Key = "chave-de-teste-com-mais-de-32-caracteres-ok",
                Issuer = "brassario",
                ExpiresMinutes = 30,
                RefreshDays = 30
            });
            var email = new EmailFalso();
            var servico = new AuthService(db, Hasher, new JwtService(jwt), jwt, relogio, email, Options.Create(new AppOptions()));

            return (servico, db, email, relogio, ana.Id);
        }

        private static AlterarSenhaRequest Troca(string atual = "SenhaAtual@1", string nova = "NovaSenha@2026", string? confirmar = null) =>
            new() { SenhaAtual = atual, NovaSenha = nova, ConfirmarSenha = confirmar ?? nova };

        private static AlterarEmailRecuperacaoRequest Email(string email) => new() { EmailRecuperacao = email };

        private static string TokenDoEmail(EmailFalso email)
        {
            var m = Regex.Match(email.Enviados.Last().Corpo, @"confirmar-email\?token=([^""&]+)");
            Assert.True(m.Success, "O e-mail deveria ter o link de confirmação.");
            return Uri.UnescapeDataString(m.Groups[1].Value);
        }

        private static void AbrirSessao(AppDbContext db, long usuarioId, string token, DateTime agora) =>
            db.RefreshTokens.Add(new RefreshToken { UsuarioId = usuarioId, TokenHash = RefreshTokenHelper.Hash(token), ExpiraEm = agora.AddDays(5) });

        // ---------- alterar-senha ----------

        [Fact]
        public async Task Senha_atual_certa_troca_a_senha()
        {
            var (servico, db, _, _, id) = Criar();

            var ok = await servico.AlterarSenhaAsync(id, Troca(), null);

            Assert.True(ok);
            var hash = db.Usuarios.Single(u => u.Id == id).SenhaHash;
            Assert.True(Hasher.Verify("NovaSenha@2026", hash));
            Assert.False(Hasher.Verify("SenhaAtual@1", hash));
            Assert.StartsWith("$2", hash);
        }

        [Fact]
        public async Task Senha_atual_errada_devolve_422_e_nao_troca_nada()
        {
            var (servico, db, _, relogio, id) = Criar();
            AbrirSessao(db, id, "a", relogio.Agora);
            db.SaveChanges();
            var hashAntes = db.Usuarios.Single(u => u.Id == id).SenhaHash;

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.AlterarSenhaAsync(id, Troca(atual: "Errada@123"), null));

            Assert.Equal("SENHA_ATUAL_INCORRETA", ex.Codigo);
            Assert.Equal(hashAntes, db.Usuarios.Single(u => u.Id == id).SenhaHash);
            Assert.All(db.RefreshTokens, t => Assert.False(t.Revogado));
        }

        [Fact]
        public async Task Nova_senha_fraca_e_validacao()
        {
            var (servico, _, _, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.AlterarSenhaAsync(id, Troca(nova: "curta"), null));

            Assert.Contains("novaSenha", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Confirmacao_diferente_e_validacao()
        {
            var (servico, _, _, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.AlterarSenhaAsync(id, Troca(confirmar: "Outra@2026"), null));

            Assert.Contains("confirmarSenha", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Nova_senha_igual_a_atual_e_validacao()
        {
            var (servico, _, _, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.AlterarSenhaAsync(id, Troca(nova: "SenhaAtual@1"), null));

            Assert.Contains("novaSenha", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Revoga_os_outros_refresh_tokens_e_mantem_o_da_sessao_atual()
        {
            var (servico, db, _, relogio, id) = Criar();
            AbrirSessao(db, id, "este-aparelho", relogio.Agora);
            AbrirSessao(db, id, "celular", relogio.Agora);
            AbrirSessao(db, id, "tablet", relogio.Agora);
            db.SaveChanges();

            await servico.AlterarSenhaAsync(id, Troca(), "este-aparelho");

            Assert.False(db.RefreshTokens.Single(t => t.TokenHash == RefreshTokenHelper.Hash("este-aparelho")).Revogado);
            Assert.True(db.RefreshTokens.Single(t => t.TokenHash == RefreshTokenHelper.Hash("celular")).Revogado);
            Assert.True(db.RefreshTokens.Single(t => t.TokenHash == RefreshTokenHelper.Hash("tablet")).Revogado);
        }

        [Fact]
        public async Task Sem_cookie_da_sessao_atual_revoga_todos()
        {
            var (servico, db, _, relogio, id) = Criar();
            AbrirSessao(db, id, "a", relogio.Agora);
            AbrirSessao(db, id, "b", relogio.Agora);
            db.SaveChanges();

            await servico.AlterarSenhaAsync(id, Troca(), null);

            Assert.All(db.RefreshTokens, t => Assert.True(t.Revogado));
        }

        [Fact]
        public async Task Usuario_inexistente_ou_inativo_devolve_false()
        {
            var (servico, db, _, _, _) = Criar();
            var inativo = db.Usuarios.Single(u => u.Email == "inativo@brassario.local").Id;

            Assert.False(await servico.AlterarSenhaAsync(9999, Troca(), null));
            Assert.False(await servico.AlterarSenhaAsync(inativo, Troca(), null));
        }

        [Fact]
        public void O_dto_nao_mostra_senhas_no_ToString()
        {
            var texto = Troca("SegredoAtual@1", "SegredoNovo@1").ToString();

            Assert.DoesNotContain("SegredoAtual@1", texto);
            Assert.DoesNotContain("SegredoNovo@1", texto);
        }

        // ---------- email-recuperacao ----------

        [Fact]
        public async Task Trocar_o_email_zera_a_confirmacao_e_envia_o_link_para_o_endereco_novo()
        {
            var (servico, db, email, relogio, id) = Criar();

            var ok = await servico.AlterarEmailRecuperacaoAsync(id, Email("Novo@Gmail.com "));

            Assert.True(ok);
            var ana = db.Usuarios.Single(u => u.Id == id);
            Assert.Equal("novo@gmail.com", ana.EmailRecuperacao);
            Assert.Null(ana.EmailRecuperacaoConfirmadoEm);

            var enviado = Assert.Single(email.Enviados);
            Assert.Equal("novo@gmail.com", enviado.Para);

            var registro = Assert.Single(db.TokensUsuario);
            Assert.Equal(TipoTokenUsuario.CONFIRMAR_EMAIL, registro.Tipo);
            Assert.Equal("novo@gmail.com", registro.EmailDestino);
            Assert.Equal(relogio.Agora.AddHours(24), registro.ExpiraEm);
            Assert.Equal(64, registro.TokenHash.Length);
            Assert.NotEqual(TokenDoEmail(email), registro.TokenHash);
        }

        [Fact]
        public async Task O_link_do_endereco_novo_confirma_o_email()
        {
            var (servico, db, email, _, id) = Criar();
            await servico.AlterarEmailRecuperacaoAsync(id, Email("novo@gmail.com"));

            var ok = await servico.ConfirmarEmailAsync(TokenDoEmail(email));

            Assert.True(ok);
            Assert.NotNull(db.Usuarios.Single(u => u.Id == id).EmailRecuperacaoConfirmadoEm);
        }

        [Fact]
        public async Task Um_pedido_novo_invalida_o_link_anterior()
        {
            var (servico, _, email, _, id) = Criar();
            await servico.AlterarEmailRecuperacaoAsync(id, Email("primeiro@gmail.com"));
            var tokenAntigo = TokenDoEmail(email);

            await servico.AlterarEmailRecuperacaoAsync(id, Email("segundo@gmail.com"));

            Assert.False(await servico.ConfirmarEmailAsync(tokenAntigo));
            Assert.True(await servico.ConfirmarEmailAsync(TokenDoEmail(email)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("isso-nao-e-email")]
        [InlineData("ana@brassario.local")]       // igual ao e-mail de login
        [InlineData("ANA@BRASSARIO.LOCAL")]
        [InlineData("antigo@gmail.com")]          // já é o e-mail de recuperação confirmado
        public async Task Email_invalido_igual_ao_login_ou_ao_atual_e_validacao_e_nao_envia_nada(string digitado)
        {
            var (servico, db, email, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.AlterarEmailRecuperacaoAsync(id, Email(digitado)));

            Assert.Contains("emailRecuperacao", ex.Detalhes.Keys);
            Assert.Empty(email.Enviados);
            Assert.Empty(db.TokensUsuario);
            Assert.NotNull(db.Usuarios.Single(u => u.Id == id).EmailRecuperacaoConfirmadoEm);   // nada mudou
        }

        [Fact]
        public async Task Email_com_mais_de_160_caracteres_e_validacao()
        {
            var (servico, _, _, _, id) = Criar();
            var longo = new string('a', 155) + "@x.com";

            await Assert.ThrowsAsync<ValidacaoException>(() => servico.AlterarEmailRecuperacaoAsync(id, Email(longo)));
        }

        [Fact]
        public async Task Email_de_recuperacao_usuario_inexistente_ou_inativo_devolve_false()
        {
            var (servico, db, _, _, _) = Criar();
            var inativo = db.Usuarios.Single(u => u.Email == "inativo@brassario.local").Id;

            Assert.False(await servico.AlterarEmailRecuperacaoAsync(9999, Email("novo@gmail.com")));
            Assert.False(await servico.AlterarEmailRecuperacaoAsync(inativo, Email("novo@gmail.com")));
        }
    }
}