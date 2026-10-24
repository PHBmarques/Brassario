using System.Reflection;
using Brassario.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Brassario.Tests
{
    public class MatrizPermissoesTests
    {
        // Únicos endpoints que podem ser chamados sem token
        private static readonly HashSet<string> Publicos = new()
    {
        "AuthController.Login",
        "AuthController.Refresh",
        "AuthController.ConfirmarEmail", // entra quando o refresh token existir (AUTH-4)
        "AuthController.EsqueciSenha",
        "AuthController.RedefinirSenha"
    };

        private record Acao(string Controller, string Nome, string Verbo, bool Anonima, bool ExigeLogin, HashSet<string> Perfis)
        {
            public string Chave => $"{Controller}.{Nome}";
            public bool SoAdmin => ExigeLogin && Perfis.Count == 1 && Perfis.Contains("ADMIN");
        }

        private static IEnumerable<Acao> Acoes()
        {
            var controllers = typeof(ReceitasController).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

            foreach (var controller in controllers)
            {
                var atributosClasse = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).ToList();

                foreach (var metodo in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    var http = metodo.GetCustomAttributes<HttpMethodAttribute>().FirstOrDefault();
                    if (http is null) continue;

                    var anonima = metodo.GetCustomAttributes<AllowAnonymousAttribute>().Any();
                    var atributosMetodo = metodo.GetCustomAttributes<AuthorizeAttribute>().ToList();
                    var todos = atributosClasse.Concat(atributosMetodo).ToList();

                    // Cada [Authorize] com Roles restringe mais: o usuário precisa satisfazer todos.
                    // Para a matriz basta saber quais perfis passam: interseção dos Roles informados.
                    HashSet<string>? perfis = null;
                    foreach (var a in todos.Where(a => !string.IsNullOrWhiteSpace(a.Roles)))
                    {
                        var roles = a.Roles!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                        perfis = perfis is null ? roles : perfis.Intersect(roles).ToHashSet();
                    }

                    yield return new Acao(
                        controller.Name, metodo.Name, http.HttpMethods.First(),
                        anonima, ExigeLogin: todos.Count > 0 && !anonima,
                        Perfis: perfis ?? new HashSet<string> { "ADMIN", "OPERADOR" });
                }
            }
        }

        [Fact]
        public void Existem_endpoints_para_conferir()
        {
            Assert.True(Acoes().Count() >= 20);
        }

        [Fact]
        public void Todo_endpoint_exige_login_exceto_os_publicos()
        {
            var problemas = Acoes()
                .Where(a => !a.ExigeLogin && !Publicos.Contains(a.Chave))
                .Select(a => $"{a.Chave} está aberto sem [Authorize]")
                .ToList();

            Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
        }

        [Fact]
        public void Somente_login_e_refresh_podem_ser_anonimos()
        {
            var problemas = Acoes()
                .Where(a => a.Anonima && !Publicos.Contains(a.Chave))
                .Select(a => $"{a.Chave} tem [AllowAnonymous] e não deveria")
                .ToList();

            Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
        }

        [Theory]
        [InlineData("InsumosController")]
        [InlineData("ReceitasController")]
        [InlineData("FornecedoresController")]
        public void Cadastros_so_podem_ser_alterados_por_admin(string controller)
        {
            var escritas = Acoes().Where(a => a.Controller == controller && a.Verbo != "GET").ToList();

            Assert.NotEmpty(escritas);
            var problemas = escritas.Where(a => !a.SoAdmin).Select(a => $"{a.Chave} ({a.Verbo}) precisa de [Authorize(Roles = \"ADMIN\")]");
            Assert.True(!problemas.Any(), string.Join(Environment.NewLine, problemas));
        }

        [Theory]
        [InlineData("InsumosController")]
        [InlineData("ReceitasController")]
        [InlineData("FornecedoresController")]
        public void Consulta_dos_cadastros_e_liberada_para_o_operador(string controller)
        {
            var consultas = Acoes().Where(a => a.Controller == controller && a.Verbo == "GET").ToList();

            Assert.NotEmpty(consultas);
            Assert.All(consultas, a =>
            {
                Assert.True(a.ExigeLogin, $"{a.Chave} precisa de login");
                Assert.Contains("OPERADOR", a.Perfis);
            });
        }

        [Fact]
        public void Gerenciar_usuarios_e_ajustar_saldo_sao_so_admin()
        {
            // Passa mesmo antes de esses endpoints existirem; passa a valer quando forem criados (AUTH-5 e API de ajustes)
            var sensiveis = Acoes().Where(a =>
                a.Controller == "UsuariosController" ||
                a.Nome.Contains("Ajust", StringComparison.OrdinalIgnoreCase)).ToList();

            var problemas = sensiveis.Where(a => !a.SoAdmin).Select(a => $"{a.Chave} precisa ser só ADMIN");
            Assert.True(!problemas.Any(), string.Join(Environment.NewLine, problemas));
        }

        [Theory]
        [InlineData("BateladasController", "Produzir")]
        [InlineData("LotesController", "RegistrarEntrada")]
        [InlineData("InsumosController", "Listar")]          // API-17: busca de insumos para a entrada
        [InlineData("FornecedoresController", "Listar")]     // API-17: busca de fornecedores para a entrada
        [InlineData("DisponibilidadeController", "Obter")]
        [InlineData("ArmarioController", "Listar")]
        [InlineData("AuthController", "Me")]
        [InlineData("AuthController", "AlterarSenha")]
        [InlineData("AuthController", "AlterarEmailRecuperacao")]
        [InlineData("AuthController", "ReenviarConfirmacao")]
        public void Operacao_do_dia_a_dia_e_liberada_para_o_operador(string controller, string acao)
        {
            var a = Acoes().Single(x => x.Controller == controller && x.Nome == acao);

            Assert.True(a.ExigeLogin, $"{a.Chave} precisa de login");
            Assert.Contains("OPERADOR", a.Perfis);
            Assert.Contains("ADMIN", a.Perfis);
        }

        [Fact]
        public void Nenhum_endpoint_e_exclusivo_do_operador()
        {
            // O ADMIN sempre pode tudo o que o OPERADOR pode (matriz 4.3)
            var problemas = Acoes()
                .Where(a => a.ExigeLogin && !a.Perfis.Contains("ADMIN"))
                .Select(a => $"{a.Chave} bloqueia o ADMIN");

            Assert.True(!problemas.Any(), string.Join(Environment.NewLine, problemas));
        }
    }
}
