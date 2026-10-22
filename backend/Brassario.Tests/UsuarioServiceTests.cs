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
    public class UsuarioServiceTests
    {
        private static readonly IPasswordHasher Hasher = new PasswordHasher();

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => new(2026, 10, 20);
            public DateTime AgoraUtc => new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        }

        private static (UsuarioService Servico, AppDbContext Db, long AdminId) Criar()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var admin = new Usuario { Nome = "Admin", Email = "admin@brassario.com", SenhaHash = Hasher.Hash("Admin@2026"), Perfil = PerfilUsuario.ADMIN, Ativo = true };
            db.Usuarios.Add(admin);
            db.SaveChanges();

            return (new UsuarioService(new UsuarioRepository(db), Hasher), db, admin.Id);
        }

        private static UsuarioRequest Novo(string nome = "João Operador", string email = "joao@brassario.com",
            string perfil = "OPERADOR", string senha = "Senha@2026") =>
            new() { Nome = nome, Email = email, Perfil = perfil, Senha = senha };

        private static UsuarioAtualizarRequest Edicao(string nome = "João Operador", string email = "joao@brassario.com",
            string perfil = "OPERADOR", string? senha = null) =>
            new() { Nome = nome, Email = email, Perfil = perfil, Senha = senha };

        // ---------- Cadastro ----------

        [Fact]
        public async Task Cria_usuario_ativo_com_senha_em_hash_bcrypt()
        {
            var (servico, db, _) = Criar();

            var r = await servico.CriarAsync(Novo());

            Assert.True(r.Id > 0);
            Assert.Equal("João Operador", r.Nome);
            Assert.Equal(PerfilUsuario.OPERADOR, r.Perfil);
            Assert.True(r.Ativo);

            var salvo = db.Usuarios.Single(u => u.Id == r.Id);
            Assert.NotEqual("Senha@2026", salvo.SenhaHash);          // nunca a senha em si
            Assert.StartsWith("$2", salvo.SenhaHash);                 // formato BCrypt
            Assert.True(Hasher.Verify("Senha@2026", salvo.SenhaHash));
        }

        [Fact]
        public async Task Email_e_guardado_sem_espacos_e_em_minusculas()
        {
            var (servico, db, _) = Criar();

            var r = await servico.CriarAsync(Novo(email: "  Joao.Silva@Brassario.COM "));

            Assert.Equal("joao.silva@brassario.com", r.Email);
            Assert.Equal("joao.silva@brassario.com", db.Usuarios.Single(u => u.Id == r.Id).Email);
        }

        [Fact]
        public async Task Perfil_aceita_minusculas()
        {
            var (servico, _, _) = Criar();

            var r = await servico.CriarAsync(Novo(perfil: "admin"));

            Assert.Equal(PerfilUsuario.ADMIN, r.Perfil);
        }

        [Theory]
        [InlineData("joao@brassario.com")]
        [InlineData("JOAO@BRASSARIO.COM")]
        [InlineData("  joao@brassario.com ")]
        public async Task Email_repetido_gera_409_sem_diferenciar_maiusculas(string email)
        {
            var (servico, db, _) = Criar();
            await servico.CriarAsync(Novo());

            await Assert.ThrowsAsync<ConflitoException>(() => servico.CriarAsync(Novo(nome: "Outro", email: email)));
            Assert.Equal(2, db.Usuarios.Count());   // admin + João
        }

        [Fact]
        public async Task Email_do_admin_do_seed_tambem_nao_pode_ser_reutilizado()
        {
            var (servico, _, _) = Criar();

            await Assert.ThrowsAsync<ConflitoException>(() => servico.CriarAsync(Novo(email: "admin@brassario.com")));
        }

        [Theory]
        [InlineData("1234567")]       // 7 caracteres
        [InlineData("curta")]
        public async Task Senha_com_menos_de_8_caracteres_gera_400(string senha)
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(Novo(senha: senha)));
            Assert.True(ex.Detalhes.ContainsKey("senha"));
        }

        [Fact]
        public async Task Senha_vazia_gera_400()
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(Novo(senha: "")));
            Assert.True(ex.Detalhes.ContainsKey("senha"));
        }

        [Theory]
        [InlineData("SUPERUSUARIO")]
        [InlineData("")]
        public async Task Perfil_invalido_gera_400_listando_as_opcoes(string perfil)
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(Novo(perfil: perfil)));
            Assert.Contains("ADMIN", ex.Detalhes["perfil"][0]);
            Assert.Contains("OPERADOR", ex.Detalhes["perfil"][0]);
        }

        [Theory]
        [InlineData("isso-nao-e-email")]
        [InlineData("sem-arroba.com")]
        [InlineData("")]
        public async Task Email_invalido_gera_400(string email)
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(Novo(email: email)));
            Assert.True(ex.Detalhes.ContainsKey("email"));
        }

        [Fact]
        public async Task Reune_todos_os_erros_de_uma_vez()
        {
            var (servico, _, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.CriarAsync(new UsuarioRequest { Nome = "", Email = "x", Perfil = "?", Senha = "1" }));

            Assert.Equal(4, ex.Detalhes.Count);   // nome, e-mail, perfil e senha
        }

        [Fact]
        public async Task Usuario_cadastrado_consegue_fazer_login()
        {
            var (servico, db, _) = Criar();
            await servico.CriarAsync(Novo(email: "maria@brassario.com", senha: "Maria@2026"));

            var jwtOpcoes = Options.Create(new JwtOptions { Key = "chave-de-teste-com-mais-de-32-caracteres-ok", Issuer = "brassario" });
            var auth = new AuthService(db, Hasher, new JwtService(jwtOpcoes), jwtOpcoes, new RelogioFixo(),
     new EmailFalso(), Options.Create(new AppOptions()));

            var sessao = await auth.IniciarSessaoAsync(new LoginRequest { Email = "maria@brassario.com", Senha = "Maria@2026" });

            Assert.NotNull(sessao);
            Assert.Equal(PerfilUsuario.OPERADOR, sessao!.Resposta.Usuario.Perfil);
            Assert.Null(await auth.IniciarSessaoAsync(new LoginRequest { Email = "maria@brassario.com", Senha = "errada123" }));
        }

        // ---------- Consulta ----------

        [Fact]
        public async Task Lista_em_ordem_de_nome_e_filtra_por_nome_ou_email()
        {
            var (servico, _, _) = Criar();
            await servico.CriarAsync(Novo(nome: "Zeca", email: "zeca@brassario.com"));
            await servico.CriarAsync(Novo(nome: "Ana", email: "ana@brassario.com"));

            var todos = await servico.ListarAsync(null, true);
            var porNome = await servico.ListarAsync("  zec ", true);
            var porEmail = await servico.ListarAsync("ANA@", true);

            Assert.Equal(new[] { "Admin", "Ana", "Zeca" }, todos.Select(u => u.Nome).ToArray());
            Assert.Equal("Zeca", Assert.Single(porNome).Nome);
            Assert.Equal("Ana", Assert.Single(porEmail).Nome);
        }

        [Fact]
        public async Task Lista_pode_esconder_os_inativos()
        {
            var (servico, _, adminId) = Criar();
            var joao = await servico.CriarAsync(Novo());
            await servico.AlterarStatusAsync(joao.Id, false, adminId);

            Assert.Equal(2, (await servico.ListarAsync(null, true)).Count);
            Assert.Single(await servico.ListarAsync(null, false));
        }

        [Fact]
        public async Task Obter_inexistente_gera_404()
        {
            var (servico, _, _) = Criar();

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterAsync(999));
        }

        // ---------- Edição ----------

        [Fact]
        public async Task Edita_nome_email_e_perfil_sem_mexer_na_senha()
        {
            var (servico, db, _) = Criar();
            var criado = await servico.CriarAsync(Novo());
            var hashAntes = db.Usuarios.Single(u => u.Id == criado.Id).SenhaHash;

            var r = await servico.AtualizarAsync(criado.Id, Edicao(nome: "João Silva", email: "joao.silva@brassario.com", perfil: "ADMIN"));

            Assert.Equal("João Silva", r.Nome);
            Assert.Equal("joao.silva@brassario.com", r.Email);
            Assert.Equal(PerfilUsuario.ADMIN, r.Perfil);
            Assert.Equal(hashAntes, db.Usuarios.Single(u => u.Id == criado.Id).SenhaHash);
        }

        [Fact]
        public async Task Manter_o_proprio_email_na_edicao_nao_da_conflito()
        {
            var (servico, _, _) = Criar();
            var criado = await servico.CriarAsync(Novo());

            var r = await servico.AtualizarAsync(criado.Id, Edicao(nome: "Novo Nome", email: "JOAO@brassario.com"));

            Assert.Equal("Novo Nome", r.Nome);
        }

        [Fact]
        public async Task Editar_para_o_email_de_outro_usuario_gera_409()
        {
            var (servico, _, _) = Criar();
            var joao = await servico.CriarAsync(Novo());

            await Assert.ThrowsAsync<ConflitoException>(() =>
                servico.AtualizarAsync(joao.Id, Edicao(email: "admin@brassario.com")));
        }

        [Fact]
        public async Task Senha_informada_na_edicao_troca_o_hash_e_encerra_as_sessoes()
        {
            var (servico, db, _) = Criar();
            var criado = await servico.CriarAsync(Novo());
            db.RefreshTokens.Add(new RefreshToken { UsuarioId = criado.Id, TokenHash = "hash-1", ExpiraEm = DateTime.UtcNow.AddDays(10) });
            db.RefreshTokens.Add(new RefreshToken { UsuarioId = criado.Id, TokenHash = "hash-2", ExpiraEm = DateTime.UtcNow.AddDays(10) });
            await db.SaveChangesAsync();

            await servico.AtualizarAsync(criado.Id, Edicao(senha: "NovaSenha@2026"));

            var salvo = db.Usuarios.Single(u => u.Id == criado.Id);
            Assert.True(Hasher.Verify("NovaSenha@2026", salvo.SenhaHash));
            Assert.False(Hasher.Verify("Senha@2026", salvo.SenhaHash));
            Assert.All(db.RefreshTokens.Where(t => t.UsuarioId == criado.Id), t => Assert.True(t.Revogado));
        }

        [Fact]
        public async Task Senha_nova_curta_na_edicao_gera_400()
        {
            var (servico, _, _) = Criar();
            var criado = await servico.CriarAsync(Novo());

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.AtualizarAsync(criado.Id, Edicao(senha: "123")));
            Assert.True(ex.Detalhes.ContainsKey("senha"));
        }

        [Fact]
        public async Task Editar_inexistente_gera_404()
        {
            var (servico, _, _) = Criar();

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.AtualizarAsync(999, Edicao()));
        }

        [Fact]
        public async Task Nao_rebaixa_o_ultimo_admin_ativo()
        {
            var (servico, db, adminId) = Criar();

            var ex = await Assert.ThrowsAsync<NegocioException>(() =>
                servico.AtualizarAsync(adminId, Edicao(nome: "Admin", email: "admin@brassario.com", perfil: "OPERADOR")));

            Assert.Equal("REGRA_NEGOCIO", ex.Codigo);
            Assert.Equal(PerfilUsuario.ADMIN, db.Usuarios.Single(u => u.Id == adminId).Perfil);
        }

        [Fact]
        public async Task Rebaixa_um_admin_quando_existe_outro_admin_ativo()
        {
            var (servico, _, adminId) = Criar();
            await servico.CriarAsync(Novo(nome: "Segundo Admin", email: "admin2@brassario.com", perfil: "ADMIN"));

            var r = await servico.AtualizarAsync(adminId, Edicao(nome: "Admin", email: "admin@brassario.com", perfil: "OPERADOR"));

            Assert.Equal(PerfilUsuario.OPERADOR, r.Perfil);
        }

        // ---------- Status ----------

        [Fact]
        public async Task Inativa_e_reativa_um_usuario()
        {
            var (servico, _, adminId) = Criar();
            var joao = await servico.CriarAsync(Novo());

            var inativo = await servico.AlterarStatusAsync(joao.Id, false, adminId);
            var ativo = await servico.AlterarStatusAsync(joao.Id, true, adminId);

            Assert.False(inativo.Ativo);
            Assert.True(ativo.Ativo);
        }

        [Fact]
        public async Task Inativar_encerra_as_sessoes_do_usuario()
        {
            var (servico, db, adminId) = Criar();
            var joao = await servico.CriarAsync(Novo());
            db.RefreshTokens.Add(new RefreshToken { UsuarioId = joao.Id, TokenHash = "hash-joao", ExpiraEm = DateTime.UtcNow.AddDays(10) });
            await db.SaveChangesAsync();

            await servico.AlterarStatusAsync(joao.Id, false, adminId);

            Assert.True(db.RefreshTokens.Single(t => t.UsuarioId == joao.Id).Revogado);
        }

        [Fact]
        public async Task Usuario_inativo_nao_consegue_entrar()
        {
            var (servico, db, adminId) = Criar();
            var joao = await servico.CriarAsync(Novo(email: "joao@brassario.com", senha: "Joao@2026"));
            await servico.AlterarStatusAsync(joao.Id, false, adminId);

            var jwtOpcoes = Options.Create(new JwtOptions { Key = "chave-de-teste-com-mais-de-32-caracteres-ok", Issuer = "brassario" });
            var auth = new AuthService(db, Hasher, new JwtService(jwtOpcoes), jwtOpcoes, new RelogioFixo(),
    new EmailFalso(), Options.Create(new AppOptions()));

            Assert.Null(await auth.IniciarSessaoAsync(new LoginRequest { Email = "joao@brassario.com", Senha = "Joao@2026" }));
        }

        [Fact]
        public async Task Ninguem_inativa_a_propria_conta()
        {
            var (servico, db, adminId) = Criar();
            await servico.CriarAsync(Novo(nome: "Segundo Admin", email: "admin2@brassario.com", perfil: "ADMIN"));

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.AlterarStatusAsync(adminId, false, adminId));

            Assert.Equal("REGRA_NEGOCIO", ex.Codigo);
            Assert.True(db.Usuarios.Single(u => u.Id == adminId).Ativo);
        }

        [Fact]
        public async Task Nao_inativa_o_ultimo_admin_ativo()
        {
            var (servico, db, adminId) = Criar();
            var operador = await servico.CriarAsync(Novo());   // o operador não é admin

            // quem executa é outro usuário (id diferente do alvo) e o alvo é o único ADMIN ativo
            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.AlterarStatusAsync(adminId, false, operador.Id));

            Assert.Equal("REGRA_NEGOCIO", ex.Codigo);
            Assert.True(db.Usuarios.Single(u => u.Id == adminId).Ativo);
        }

        [Fact]
        public async Task Inativa_um_admin_quando_existe_outro_admin_ativo()
        {
            var (servico, _, adminId) = Criar();
            var segundo = await servico.CriarAsync(Novo(nome: "Segundo Admin", email: "admin2@brassario.com", perfil: "ADMIN"));

            var r = await servico.AlterarStatusAsync(segundo.Id, false, adminId);

            Assert.False(r.Ativo);
        }

        [Fact]
        public async Task Alterar_status_de_inexistente_gera_404()
        {
            var (servico, _, adminId) = Criar();

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.AlterarStatusAsync(999, false, adminId));
        }
    }
}
