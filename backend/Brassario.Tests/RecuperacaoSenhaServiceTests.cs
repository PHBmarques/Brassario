using System.Text.RegularExpressions;
using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Security;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Brassario.Tests
{
    public class RecuperacaoSenhaServiceTests
    {
        private static readonly IPasswordHasher Hasher = new PasswordHasher();

        private class RelogioMutavel : IRelogio
        {
            public DateTime Agora { get; set; } = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
            public DateOnly Hoje => DateOnly.FromDateTime(Agora);
            public DateTime AgoraUtc => Agora;
        }

        private static (RecuperacaoSenhaService Servico, AppDbContext Db, EmailFalso Email, RelogioMutavel Relogio) Criar(int limite = 3)
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var agora = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
            db.Usuarios.AddRange(
                new Usuario
                {
                    Nome = "Ana",
                    Email = "ana@brassario.local",
                    SenhaHash = Hasher.Hash("SenhaAntiga@1"),
                    Perfil = PerfilUsuario.OPERADOR,
                    Ativo = true,
                    EmailRecuperacao = "ana@gmail.com",
                    EmailRecuperacaoConfirmadoEm = agora
                },
                new Usuario
                {
                    Nome = "Sem confirmar",
                    Email = "semconf@brassario.local",
                    SenhaHash = Hasher.Hash("SenhaAntiga@1"),
                    Ativo = true,
                    EmailRecuperacao = "semconf@gmail.com",
                    EmailRecuperacaoConfirmadoEm = null
                },
                new Usuario { Nome = "Sem e-mail", Email = "sememail@brassario.local", SenhaHash = Hasher.Hash("SenhaAntiga@1"), Ativo = true },
                new Usuario
                {
                    Nome = "Inativo",
                    Email = "inativo@brassario.local",
                    SenhaHash = Hasher.Hash("SenhaAntiga@1"),
                    Ativo = false,
                    EmailRecuperacao = "inativo@gmail.com",
                    EmailRecuperacaoConfirmadoEm = agora
                });
            db.SaveChanges();

            var relogio = new RelogioMutavel();
            var email = new EmailFalso();
            var servico = new RecuperacaoSenhaService(db, Hasher, relogio, email, new LimitadorTentativas(relogio),
                Options.Create(new AppOptions()), Options.Create(new SegurancaOptions { EsqueciSenhaPorHora = limite }),
                NullLogger<RecuperacaoSenhaService>.Instance);

            return (servico, db, email, relogio);
        }

        // O token só existe no link do e-mail: é de lá que o teste o tira
        private static string TokenDoEmail(EmailFalso email, int indice = -1)
        {
            var corpo = (indice < 0 ? email.Enviados.Last() : email.Enviados[indice]).Corpo;
            var m = Regex.Match(corpo, @"redefinir-senha\?token=([^""&]+)");
            Assert.True(m.Success, "O e-mail deveria ter o link de redefinição.");
            return Uri.UnescapeDataString(m.Groups[1].Value);
        }

        private static RedefinirSenhaRequest Pedido(string token, string senha = "NovaSenha@2026", string? confirmar = null) =>
            new() { Token = token, NovaSenha = senha, ConfirmarSenha = confirmar ?? senha };

        // ---------- esqueci-senha ----------

        [Fact]
        public async Task Conta_valida_recebe_o_link_no_email_de_recuperacao()
        {
            var (servico, db, email, relogio) = Criar();

            await servico.SolicitarAsync("ana@brassario.local");

            var enviado = Assert.Single(email.Enviados);
            Assert.Equal("ana@gmail.com", enviado.Para);
            Assert.Contains("/redefinir-senha?token=", enviado.Corpo);

            var registro = Assert.Single(db.TokensUsuario);
            Assert.Equal(TipoTokenUsuario.REDEFINIR_SENHA, registro.Tipo);
            Assert.Equal(relogio.Agora.AddMinutes(30), registro.ExpiraEm);
            Assert.Equal("ana@gmail.com", registro.EmailDestino);
        }

        [Fact]
        public async Task O_banco_guarda_so_o_hash_do_token()
        {
            var (servico, db, email, _) = Criar();

            await servico.SolicitarAsync("ana@brassario.local");

            var token = TokenDoEmail(email);
            var registro = db.TokensUsuario.Single();
            Assert.NotEqual(token, registro.TokenHash);
            Assert.Equal(RefreshTokenHelper.Hash(token), registro.TokenHash);
            Assert.Equal(64, registro.TokenHash.Length);
        }

        [Theory]
        [InlineData("ANA@BRASSARIO.LOCAL")]
        [InlineData("  ana@brassario.local  ")]
        public async Task O_email_de_login_nao_diferencia_maiusculas_nem_espacos(string digitado)
        {
            var (servico, _, email, _) = Criar();

            await servico.SolicitarAsync(digitado);

            Assert.Single(email.Enviados);
        }

        [Theory]
        [InlineData("naoexiste@brassario.local")]     // conta inexistente
        [InlineData("inativo@brassario.local")]       // conta inativa
        [InlineData("sememail@brassario.local")]      // sem e-mail de recuperação
        [InlineData("semconf@brassario.local")]       // e-mail de recuperação não confirmado
        public async Task Nao_envia_nada_e_nao_acusa_erro_quando_nao_pode_enviar(string digitado)
        {
            var (servico, db, email, _) = Criar();

            await servico.SolicitarAsync(digitado);

            Assert.Empty(email.Enviados);
            Assert.Empty(db.TokensUsuario);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Email_em_branco_e_validacao(string? digitado)
        {
            var (servico, _, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.SolicitarAsync(digitado));
            Assert.Contains("email", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Novo_pedido_invalida_o_link_anterior()
        {
            var (servico, db, email, _) = Criar();

            await servico.SolicitarAsync("ana@brassario.local");
            var primeiro = TokenDoEmail(email);
            await servico.SolicitarAsync("ana@brassario.local");
            var segundo = TokenDoEmail(email);

            Assert.False(await servico.RedefinirAsync(Pedido(primeiro)));
            Assert.True(await servico.RedefinirAsync(Pedido(segundo)));
        }

        // ---------- limite de tentativas ----------

        [Fact]
        public async Task Quarto_pedido_na_mesma_hora_estoura_o_limite()
        {
            var (servico, _, _, _) = Criar();

            for (var i = 0; i < 3; i++)
                await servico.SolicitarAsync("ana@brassario.local");

            await Assert.ThrowsAsync<MuitasTentativasException>(() => servico.SolicitarAsync("ana@brassario.local"));
        }

        [Fact]
        public async Task O_limite_conta_tambem_para_conta_inexistente_e_nao_revela_quem_tem_cadastro()
        {
            var (servico, _, _, _) = Criar();

            for (var i = 0; i < 3; i++)
                await servico.SolicitarAsync("naoexiste@brassario.local");

            await Assert.ThrowsAsync<MuitasTentativasException>(() => servico.SolicitarAsync("naoexiste@brassario.local"));
        }

        [Fact]
        public async Task O_limite_e_por_email_de_login()
        {
            var (servico, _, _, _) = Criar();

            for (var i = 0; i < 3; i++)
                await servico.SolicitarAsync("ana@brassario.local");

            await servico.SolicitarAsync("semconf@brassario.local");   // outro e-mail continua livre
        }

        [Fact]
        public async Task Depois_de_uma_hora_o_limite_libera()
        {
            var (servico, _, _, relogio) = Criar();

            for (var i = 0; i < 3; i++)
                await servico.SolicitarAsync("ana@brassario.local");
            relogio.Agora = relogio.Agora.AddHours(1).AddSeconds(1);

            await servico.SolicitarAsync("ana@brassario.local");
        }

        // ---------- redefinir-senha ----------

        [Fact]
        public async Task Token_valido_troca_a_senha_e_zera_a_troca_pendente()
        {
            var (servico, db, email, _) = Criar();
            db.Usuarios.Single(u => u.Email == "ana@brassario.local").DeveTrocarSenha = true;
            db.SaveChanges();
            await servico.SolicitarAsync("ana@brassario.local");

            var ok = await servico.RedefinirAsync(Pedido(TokenDoEmail(email)));

            Assert.True(ok);
            var ana = db.Usuarios.Single(u => u.Email == "ana@brassario.local");
            Assert.True(Hasher.Verify("NovaSenha@2026", ana.SenhaHash));
            Assert.False(Hasher.Verify("SenhaAntiga@1", ana.SenhaHash));
            Assert.False(ana.DeveTrocarSenha);
            Assert.NotNull(db.TokensUsuario.Single().UsadoEm);
        }

        [Fact]
        public async Task Redefinir_revoga_todos_os_refresh_tokens_do_usuario()
        {
            var (servico, db, email, relogio) = Criar();
            var ana = db.Usuarios.Single(u => u.Email == "ana@brassario.local");
            db.RefreshTokens.AddRange(
                new RefreshToken { UsuarioId = ana.Id, TokenHash = RefreshTokenHelper.Hash("a"), ExpiraEm = relogio.Agora.AddDays(5) },
                new RefreshToken { UsuarioId = ana.Id, TokenHash = RefreshTokenHelper.Hash("b"), ExpiraEm = relogio.Agora.AddDays(5) });
            db.SaveChanges();
            await servico.SolicitarAsync("ana@brassario.local");

            await servico.RedefinirAsync(Pedido(TokenDoEmail(email)));

            Assert.All(db.RefreshTokens.Where(t => t.UsuarioId == ana.Id), t => Assert.True(t.Revogado));
        }

        [Fact]
        public async Task Token_so_vale_uma_vez()
        {
            var (servico, _, email, _) = Criar();
            await servico.SolicitarAsync("ana@brassario.local");
            var token = TokenDoEmail(email);

            Assert.True(await servico.RedefinirAsync(Pedido(token)));
            Assert.False(await servico.RedefinirAsync(Pedido(token, "OutraSenha@2026")));
        }

        [Fact]
        public async Task Token_vence_em_30_minutos()
        {
            var (servico, _, email, relogio) = Criar();
            await servico.SolicitarAsync("ana@brassario.local");
            var token = TokenDoEmail(email);

            relogio.Agora = relogio.Agora.AddMinutes(31);

            Assert.False(await servico.RedefinirAsync(Pedido(token)));
        }

        [Fact]
        public async Task Token_ainda_dentro_dos_30_minutos_e_aceito()
        {
            var (servico, _, email, relogio) = Criar();
            await servico.SolicitarAsync("ana@brassario.local");
            var token = TokenDoEmail(email);

            relogio.Agora = relogio.Agora.AddMinutes(29);

            Assert.True(await servico.RedefinirAsync(Pedido(token)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("token-que-nao-existe")]
        public async Task Token_vazio_ou_desconhecido_e_recusado(string token)
        {
            var (servico, _, _, _) = Criar();

            Assert.False(await servico.RedefinirAsync(Pedido(token)));
        }

        [Fact]
        public async Task Senha_fraca_e_validacao_e_nao_gasta_o_link()
        {
            var (servico, _, email, _) = Criar();
            await servico.SolicitarAsync("ana@brassario.local");
            var token = TokenDoEmail(email);

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.RedefinirAsync(Pedido(token, "curta")));

            Assert.Contains("novaSenha", ex.Detalhes.Keys);
            Assert.True(await servico.RedefinirAsync(Pedido(token)));   // o link continua valendo
        }

        [Fact]
        public async Task Confirmacao_diferente_e_validacao()
        {
            var (servico, _, email, _) = Criar();
            await servico.SolicitarAsync("ana@brassario.local");

            var ex = await Assert.ThrowsAsync<ValidacaoException>(
                () => servico.RedefinirAsync(Pedido(TokenDoEmail(email), "NovaSenha@2026", "Diferente@2026")));

            Assert.Contains("confirmarSenha", ex.Detalhes.Keys);
        }

        [Fact]
        public async Task Link_nao_vale_se_o_email_de_recuperacao_mudou_depois_do_pedido()
        {
            var (servico, db, email, _) = Criar();
            await servico.SolicitarAsync("ana@brassario.local");
            var token = TokenDoEmail(email);
            db.Usuarios.Single(u => u.Email == "ana@brassario.local").EmailRecuperacao = "outro@gmail.com";
            db.SaveChanges();

            Assert.False(await servico.RedefinirAsync(Pedido(token)));
        }

        [Fact]
        public async Task Link_nao_vale_para_conta_inativada_depois_do_pedido()
        {
            var (servico, db, email, _) = Criar();
            await servico.SolicitarAsync("ana@brassario.local");
            var token = TokenDoEmail(email);
            db.Usuarios.Single(u => u.Email == "ana@brassario.local").Ativo = false;
            db.SaveChanges();

            Assert.False(await servico.RedefinirAsync(Pedido(token)));
        }

        [Fact]
        public async Task O_dto_nao_mostra_token_nem_senha_no_ToString()
        {
            var texto = Pedido("segredo-do-token", "SenhaSecreta@1").ToString();

            Assert.DoesNotContain("segredo-do-token", texto);
            Assert.DoesNotContain("SenhaSecreta@1", texto);
            await Task.CompletedTask;
        }

        // ---------- reenviar-confirmacao (AUTH-14) ----------

        private static long IdDe(AppDbContext db, string emailLogin) => db.Usuarios.Single(u => u.Email == emailLogin).Id;

        private static string TokenDeConfirmacao(EmailFalso email)
        {
            var m = Regex.Match(email.Enviados.Last().Corpo, @"confirmar-email\?token=([^""&]+)");
            Assert.True(m.Success, "O e-mail deveria ter o link de confirmação.");
            return Uri.UnescapeDataString(m.Groups[1].Value);
        }

        [Fact]
        public async Task Reenvio_com_email_pendente_envia_o_link_para_o_endereco_atual()
        {
            var (servico, db, email, relogio) = Criar();

            var ok = await servico.ReenviarConfirmacaoAsync(IdDe(db, "semconf@brassario.local"));

            Assert.True(ok);
            var enviado = Assert.Single(email.Enviados);
            Assert.Equal("semconf@gmail.com", enviado.Para);
            Assert.Contains("/confirmar-email?token=", enviado.Corpo);

            var registro = Assert.Single(db.TokensUsuario);
            Assert.Equal(TipoTokenUsuario.CONFIRMAR_EMAIL, registro.Tipo);
            Assert.Equal("semconf@gmail.com", registro.EmailDestino);
            Assert.Equal(relogio.Agora.AddHours(24), registro.ExpiraEm);
            Assert.Equal(64, registro.TokenHash.Length);
            Assert.NotEqual(TokenDeConfirmacao(email), registro.TokenHash);   // o banco guarda só o hash
        }

        [Fact]
        public async Task Reenvio_invalida_o_link_pendente_anterior()
        {
            var (servico, db, email, _) = Criar();
            var id = IdDe(db, "semconf@brassario.local");

            await servico.ReenviarConfirmacaoAsync(id);
            await servico.ReenviarConfirmacaoAsync(id);

            var tokens = db.TokensUsuario.OrderBy(t => t.Id).ToList();
            Assert.Equal(2, tokens.Count);
            Assert.NotNull(tokens[0].UsadoEm);   // o primeiro deixou de valer
            Assert.Null(tokens[1].UsadoEm);      // só o último vale
            Assert.Equal(2, email.Enviados.Count);
        }

        [Fact]
        public async Task Reenvio_sem_email_de_recuperacao_devolve_422_SEM_EMAIL_RECUPERACAO()
        {
            var (servico, db, email, _) = Criar();

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.ReenviarConfirmacaoAsync(IdDe(db, "sememail@brassario.local")));

            Assert.Equal("SEM_EMAIL_RECUPERACAO", ex.Codigo);
            Assert.Empty(email.Enviados);
            Assert.Empty(db.TokensUsuario);
        }

        [Fact]
        public async Task Reenvio_com_email_ja_confirmado_devolve_422_EMAIL_JA_CONFIRMADO()
        {
            var (servico, db, email, _) = Criar();

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.ReenviarConfirmacaoAsync(IdDe(db, "ana@brassario.local")));

            Assert.Equal("EMAIL_JA_CONFIRMADO", ex.Codigo);
            Assert.Empty(email.Enviados);
            Assert.Empty(db.TokensUsuario);
        }

        [Fact]
        public async Task Quarto_reenvio_na_mesma_hora_estoura_o_limite()
        {
            var (servico, db, email, _) = Criar();
            var id = IdDe(db, "semconf@brassario.local");

            for (var i = 0; i < 3; i++)
                await servico.ReenviarConfirmacaoAsync(id);

            await Assert.ThrowsAsync<MuitasTentativasException>(() => servico.ReenviarConfirmacaoAsync(id));
            Assert.Equal(3, email.Enviados.Count);   // o quarto não enviou nada
        }

        [Fact]
        public async Task O_limite_do_reenvio_e_por_usuario()
        {
            var (servico, db, _, _) = Criar();
            var pendente = IdDe(db, "semconf@brassario.local");
            var outro = db.Usuarios.Add(new Usuario
            {
                Nome = "Outro",
                Email = "outro@brassario.local",
                SenhaHash = Hasher.Hash("SenhaAntiga@1"),
                Ativo = true,
                EmailRecuperacao = "outro@gmail.com"
            }).Entity;
            db.SaveChanges();

            for (var i = 0; i < 3; i++)
                await servico.ReenviarConfirmacaoAsync(pendente);

            Assert.True(await servico.ReenviarConfirmacaoAsync(outro.Id));   // outro usuário continua livre
        }

        [Fact]
        public async Task Depois_de_uma_hora_o_reenvio_libera()
        {
            var (servico, db, _, relogio) = Criar();
            var id = IdDe(db, "semconf@brassario.local");

            for (var i = 0; i < 3; i++)
                await servico.ReenviarConfirmacaoAsync(id);
            relogio.Agora = relogio.Agora.AddHours(1).AddSeconds(1);

            Assert.True(await servico.ReenviarConfirmacaoAsync(id));
        }

        [Fact]
        public async Task Os_erros_422_nao_gastam_as_tentativas()
        {
            var (servico, db, _, _) = Criar();
            var ana = IdDe(db, "ana@brassario.local");   // já confirmada

            for (var i = 0; i < 5; i++)
                await Assert.ThrowsAsync<NegocioException>(() => servico.ReenviarConfirmacaoAsync(ana));

            // Se os erros contassem, estas chamadas já estariam bloqueadas pelo limite
            db.Usuarios.Single(u => u.Id == ana).EmailRecuperacaoConfirmadoEm = null;
            db.SaveChanges();
            Assert.True(await servico.ReenviarConfirmacaoAsync(ana));
        }

        [Fact]
        public async Task Reenvio_de_usuario_inexistente_ou_inativo_devolve_false()
        {
            var (servico, db, _, _) = Criar();

            Assert.False(await servico.ReenviarConfirmacaoAsync(9999));
            Assert.False(await servico.ReenviarConfirmacaoAsync(IdDe(db, "inativo@brassario.local")));
        }
    }
}