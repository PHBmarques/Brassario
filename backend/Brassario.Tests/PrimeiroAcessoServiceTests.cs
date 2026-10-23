using System.Text.RegularExpressions;
using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Extensions.Options;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>TST-8: primeiro acesso (troca da senha temporária) e confirmação do e-mail de recuperação.</summary>
    public class PrimeiroAcessoServiceTests
    {
        private static readonly IPasswordHasher Hasher = new PasswordHasher();
        private const string SenhaTemporaria = "Temporaria@123";
        private const string SenhaNova = "NovaSenha@2026";

        private class RelogioMutavel : IRelogio
        {
            public DateTime Agora { get; set; } = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
            public DateOnly Hoje => DateOnly.FromDateTime(Agora);
            public DateTime AgoraUtc => Agora;
        }

        private static (AuthService Servico, AppDbContext Db, EmailFalso Email, RelogioMutavel Relogio, long Id) Criar(bool trocaPendente = true)
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var relogio = new RelogioMutavel();
            var bia = new Usuario
            {
                Nome = "Bia",
                Email = "bia@brassario.local",
                SenhaHash = Hasher.Hash(SenhaTemporaria),
                Perfil = PerfilUsuario.OPERADOR,
                Ativo = true,
                DeveTrocarSenha = trocaPendente
            };
            db.Usuarios.Add(bia);
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
            return (servico, db, email, relogio, bia.Id);
        }

        private static PrimeiroAcessoRequest Pedido(string nova = SenhaNova, string? confirmar = null, string? emailRecuperacao = null) =>
            new() { NovaSenha = nova, ConfirmarSenha = confirmar ?? nova, EmailRecuperacao = emailRecuperacao };

        private static string TokenDoEmail(EmailFalso email)
        {
            var m = Regex.Match(email.Enviados.Last().Corpo, @"confirmar-email\?token=([^""&]+)");
            Assert.True(m.Success, "O e-mail deveria ter o link de confirmação.");
            return Uri.UnescapeDataString(m.Groups[1].Value);
        }

        // ---------- primeiro acesso: validações ----------

        [Fact]
        public async Task Senha_fraca_e_validacao_e_nao_muda_nada()
        {
            var (servico, db, email, _, id) = Criar();
            var hashAntes = db.Usuarios.Single(u => u.Id == id).SenhaHash;

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ConcluirPrimeiroAcessoAsync(id, Pedido(nova: "curta")));

            Assert.Contains("novaSenha", ex.Detalhes.Keys);
            var u = db.Usuarios.Single(x => x.Id == id);
            Assert.Equal(hashAntes, u.SenhaHash);
            Assert.True(u.DeveTrocarSenha);
            Assert.Empty(email.Enviados);
        }

        [Fact]
        public async Task Confirmacao_diferente_e_validacao()
        {
            var (servico, _, _, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ConcluirPrimeiroAcessoAsync(id, Pedido(confirmar: "Outra@2026")));

            Assert.Contains("confirmarSenha", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Nova_senha_igual_a_temporaria_e_validacao()
        {
            var (servico, _, _, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ConcluirPrimeiroAcessoAsync(id, Pedido(nova: SenhaTemporaria)));

            Assert.Contains("novaSenha", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Email_de_recuperacao_invalido_e_validacao_e_nao_envia_nada()
        {
            var (servico, db, email, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ConcluirPrimeiroAcessoAsync(id, Pedido(emailRecuperacao: "isto-nao-e-email")));

            Assert.Contains("emailRecuperacao", ex.Detalhes.Keys);
            Assert.Empty(email.Enviados);
            Assert.Empty(db.TokensUsuario);
            Assert.True(db.Usuarios.Single(u => u.Id == id).DeveTrocarSenha);
        }

        [Fact]
        public async Task Email_de_recuperacao_igual_ao_de_login_e_validacao()
        {
            var (servico, _, _, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.ConcluirPrimeiroAcessoAsync(id, Pedido(emailRecuperacao: "BIA@brassario.local")));

            Assert.Contains("emailRecuperacao", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Reune_todos_os_erros_de_uma_vez()
        {
            var (servico, _, _, _, id) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.ConcluirPrimeiroAcessoAsync(id, Pedido(nova: "curta", confirmar: "outra", emailRecuperacao: "ruim")));

            Assert.Contains("novaSenha", ex.Detalhes.Keys);
            Assert.Contains("confirmarSenha", ex.Detalhes.Keys);
            Assert.Contains("emailRecuperacao", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Sem_troca_pendente_devolve_422_TROCA_NAO_PENDENTE()
        {
            var (servico, db, _, _, id) = Criar(trocaPendente: false);
            var hashAntes = db.Usuarios.Single(u => u.Id == id).SenhaHash;

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.ConcluirPrimeiroAcessoAsync(id, Pedido()));

            Assert.Equal("TROCA_NAO_PENDENTE", ex.Codigo);
            Assert.Equal(hashAntes, db.Usuarios.Single(u => u.Id == id).SenhaHash);
        }

        [Fact]
        public async Task Usuario_inexistente_devolve_nulo()
        {
            var (servico, _, _, _, _) = Criar();

            Assert.Null(await servico.ConcluirPrimeiroAcessoAsync(9999, Pedido()));
        }

        // ---------- primeiro acesso: caminho feliz ----------

        [Fact]
        public async Task Caminho_feliz_sem_email_troca_a_senha_e_libera_a_conta()
        {
            var (servico, db, email, relogio, id) = Criar();
            db.RefreshTokens.Add(new RefreshToken { UsuarioId = id, TokenHash = RefreshTokenHelper.Hash("antigo"), ExpiraEm = relogio.Agora.AddDays(5) });
            db.SaveChanges();

            var sessao = await servico.ConcluirPrimeiroAcessoAsync(id, Pedido());

            Assert.NotNull(sessao);
            var u = db.Usuarios.Single(x => x.Id == id);
            Assert.False(u.DeveTrocarSenha);
            Assert.True(Hasher.Verify(SenhaNova, u.SenhaHash));
            Assert.False(Hasher.Verify(SenhaTemporaria, u.SenhaHash));
            Assert.StartsWith("$2", u.SenhaHash);
            Assert.Null(u.EmailRecuperacao);
            Assert.Empty(db.TokensUsuario);
            Assert.Empty(email.Enviados);
            // a sessão da senha temporária cai; só a nova fica aberta
            Assert.Contains(db.RefreshTokens, t => t.TokenHash == RefreshTokenHelper.Hash("antigo") && t.Revogado);
            // o access token novo não leva mais a claim de troca pendente
            var jwt = new JsonWebTokenHandler().ReadJsonWebToken(sessao!.Resposta.AccessToken);
            Assert.DoesNotContain(jwt.Claims, c => c.Type == "pwd_change");
        }

        [Fact]
        public async Task Caminho_feliz_com_email_guarda_o_endereco_nao_confirmado_e_envia_o_link()
        {
            var (servico, db, email, relogio, id) = Criar();

            var sessao = await servico.ConcluirPrimeiroAcessoAsync(id, Pedido(emailRecuperacao: "  Bia.Pessoal@Gmail.com "));

            Assert.NotNull(sessao);
            var u = db.Usuarios.Single(x => x.Id == id);
            Assert.False(u.DeveTrocarSenha);
            Assert.Equal("bia.pessoal@gmail.com", u.EmailRecuperacao);
            Assert.Null(u.EmailRecuperacaoConfirmadoEm);

            var mensagem = Assert.Single(email.Enviados);
            Assert.Equal("bia.pessoal@gmail.com", mensagem.Para);

            var token = TokenDoEmail(email);
            var registro = Assert.Single(db.TokensUsuario);
            Assert.Equal(TipoTokenUsuario.CONFIRMAR_EMAIL, registro.Tipo);
            Assert.Equal(RefreshTokenHelper.Hash(token), registro.TokenHash);   // só o hash vai para o banco
            Assert.NotEqual(token, registro.TokenHash);
            Assert.Equal(relogio.Agora.AddHours(24), registro.ExpiraEm);
        }

        // ---------- confirmar e-mail ----------

        private static async Task<(AuthService Servico, AppDbContext Db, RelogioMutavel Relogio, string Token, long Id)> ComLinkAsync()
        {
            var (servico, db, email, relogio, id) = Criar();
            await servico.ConcluirPrimeiroAcessoAsync(id, Pedido(emailRecuperacao: "bia.pessoal@gmail.com"));
            return (servico, db, relogio, TokenDoEmail(email), id);
        }

        [Fact]
        public async Task Confirmar_email_com_token_valido_confirma_e_gasta_o_link()
        {
            var (servico, db, relogio, token, id) = await ComLinkAsync();

            var ok = await servico.ConfirmarEmailAsync(token);

            Assert.True(ok);
            Assert.Equal(relogio.Agora, db.Usuarios.Single(u => u.Id == id).EmailRecuperacaoConfirmadoEm);
            Assert.NotNull(db.TokensUsuario.Single().UsadoEm);
        }

        [Fact]
        public async Task Confirmar_email_com_token_expirado_e_recusado()
        {
            var (servico, db, relogio, token, id) = await ComLinkAsync();
            relogio.Agora = relogio.Agora.AddHours(24).AddSeconds(1);

            var ok = await servico.ConfirmarEmailAsync(token);

            Assert.False(ok);
            Assert.Null(db.Usuarios.Single(u => u.Id == id).EmailRecuperacaoConfirmadoEm);
        }

        [Fact]
        public async Task Confirmar_email_ainda_dentro_das_24_horas_e_aceito()
        {
            var (servico, _, relogio, token, _) = await ComLinkAsync();
            relogio.Agora = relogio.Agora.AddHours(23);

            Assert.True(await servico.ConfirmarEmailAsync(token));
        }

        [Fact]
        public async Task Confirmar_email_com_token_ja_usado_e_recusado()
        {
            var (servico, _, _, token, _) = await ComLinkAsync();

            Assert.True(await servico.ConfirmarEmailAsync(token));
            Assert.False(await servico.ConfirmarEmailAsync(token));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("token-que-nunca-existiu")]
        public async Task Confirmar_email_com_token_vazio_ou_inexistente_e_recusado(string? token)
        {
            var (servico, _, _, _, _) = await ComLinkAsync();

            Assert.False(await servico.ConfirmarEmailAsync(token));
        }

        [Fact]
        public async Task Confirmar_email_de_conta_inativada_depois_do_pedido_e_recusado()
        {
            var (servico, db, _, token, id) = await ComLinkAsync();
            db.Usuarios.Single(u => u.Id == id).Ativo = false;
            db.SaveChanges();

            Assert.False(await servico.ConfirmarEmailAsync(token));
        }

        [Fact]
        public async Task Confirmar_email_com_link_de_endereco_antigo_e_recusado()
        {
            var (servico, db, _, token, id) = await ComLinkAsync();
            db.Usuarios.Single(u => u.Id == id).EmailRecuperacao = "outro@gmail.com";   // o e-mail mudou depois do pedido
            db.SaveChanges();

            Assert.False(await servico.ConfirmarEmailAsync(token));
            Assert.Null(db.Usuarios.Single(u => u.Id == id).EmailRecuperacaoConfirmadoEm);
        }
    }
}