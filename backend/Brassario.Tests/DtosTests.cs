using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Brassario.Api.Controllers;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Brassario.Tests
{
    public class DtosTests
    {
        // ---------- Validação dos Requests (DataAnnotations) ----------

        private static List<ValidationResult> Validar(object objeto)
        {
            var resultados = new List<ValidationResult>();
            Validator.TryValidateObject(objeto, new ValidationContext(objeto), resultados, validateAllProperties: true);
            return resultados;
        }

        private static string[] Mensagens(object objeto) => Validar(objeto).Select(r => r.ErrorMessage ?? "").ToArray();

        [Fact]
        public void Login_vazio_pede_email_e_senha_em_portugues()
        {
            var m = Mensagens(new LoginRequest());
            Assert.Contains("O e-mail é obrigatório.", m);
            Assert.Contains("A senha é obrigatória.", m);
        }

        [Fact]
        public void Login_com_email_invalido_e_recusado()
        {
            var m = Mensagens(new LoginRequest { Email = "isso-nao-e-email", Senha = "x" });
            Assert.Contains("Informe um e-mail válido.", m);
        }

        [Fact]
        public void Insumo_vazio_e_recusado()
        {
            var m = Mensagens(new InsumoRequest());
            Assert.Contains("O nome é obrigatório.", m);
            Assert.Contains("A categoria é obrigatória.", m);
            Assert.Contains("A unidade de medida é obrigatória.", m);
        }

        [Fact]
        public void Lote_vazio_e_recusado()
        {
            var m = Mensagens(new LoteRequest());
            Assert.Contains("Informe o insumo.", m);
            Assert.Contains("O código do lote é obrigatório.", m);
            Assert.Contains("A data de validade é obrigatória.", m);
            Assert.Contains("A quantidade deve ser maior que zero.", m);
        }

        [Fact]
        public void Receita_sem_itens_e_recusada()
        {
            var m = Mensagens(new ReceitaRequest { Nome = "IPA", VolumeBaseLitros = 100, Itens = new() });
            Assert.Contains("A receita precisa ter pelo menos 1 ingrediente.", m);
        }

        [Fact]
        public void Receita_vazia_e_recusada()
        {
            var m = Mensagens(new ReceitaRequest());
            Assert.Contains("O nome é obrigatório.", m);
            Assert.Contains("O volume base deve ser maior que zero.", m);
        }

        [Fact]
        public void Item_de_receita_invalido_e_recusado()
        {
            var m = Mensagens(new ReceitaItemRequest());
            Assert.Contains("Informe o insumo do ingrediente.", m);
            Assert.Contains("A quantidade deve ser maior que zero (mínimo 0,001).", m);
        }

        [Fact]
        public void Alterar_status_sem_valor_e_recusado()
        {
            Assert.Contains("Informe se o registro fica ativo ou inativo.", Mensagens(new AlterarStatusRequest()));
        }

        [Fact]
        public void Batelada_vazia_e_recusada()
        {
            var m = Mensagens(new BateladaRequest());
            Assert.Contains("Informe a receita.", m);
            Assert.Contains("Informe o volume a produzir, em litros.", m);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-5.0)]
        [InlineData(0.001)]
        public void Batelada_com_volume_invalido_e_recusada(double volume)
        {
            var m = Mensagens(new BateladaRequest { ReceitaId = 1, VolumePlanejado = (decimal)volume });
            Assert.Contains("O volume deve ser maior que zero (mínimo 0,01 litro).", m);
        }

        [Fact]
        public void Batelada_com_observacao_longa_e_recusada()
        {
            var m = Mensagens(new BateladaRequest { ReceitaId = 1, VolumePlanejado = 100, Observacoes = new string('x', 256) });
            Assert.Contains("As observações têm no máximo 255 caracteres.", m);
        }

        [Fact]
        public void Batelada_valida_passa()
        {
            Assert.Empty(Validar(new BateladaRequest { ReceitaId = 1, VolumePlanejado = 100, Observacoes = "Ok" }));
        }

        [Fact]
        public void Nenhuma_mensagem_de_validacao_esta_em_ingles()
        {
            var requests = new object[]
            {
            new LoginRequest(), new InsumoRequest(), new LoteRequest(), new ReceitaRequest(),
            new ReceitaItemRequest(), new AlterarStatusRequest(), new BateladaRequest()
            };

            var ingles = new[] { "field is required", "must be", "is invalid", "The " };
            foreach (var r in requests)
                foreach (var mensagem in Mensagens(r))
                    Assert.DoesNotContain(ingles, trecho => mensagem.Contains(trecho));
        }

        // ---------- Nada de entidade na entrada nem na saída ----------

        private static IEnumerable<MethodInfo> Acoes() =>
            typeof(ReceitasController).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

        private static bool EhEntidade(Type t) =>
            t.IsClass && t.Namespace == "Brassario.Api.Models";

        // Desce pelos genéricos, listas e propriedades procurando uma entidade ou campo de senha
        private static void Inspecionar(Type tipo, string origem, HashSet<Type> visitados, List<string> problemas, bool bloquearSenha)
        {
            if (tipo.IsGenericType)
            {
                foreach (var arg in tipo.GetGenericArguments())
                    Inspecionar(arg, origem, visitados, problemas, bloquearSenha);
                if (tipo.Namespace?.StartsWith("System") == true) return;
            }
            if (tipo.IsArray) { Inspecionar(tipo.GetElementType()!, origem, visitados, problemas, bloquearSenha); return; }
            if (tipo.IsEnum || tipo.IsPrimitive || tipo.Namespace?.StartsWith("System") == true) return;
            if (!visitados.Add(tipo)) return;

            if (EhEntidade(tipo))
            {
                problemas.Add($"{origem}: expõe a entidade {tipo.Name}");
                return;
            }

            foreach (var p in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var nome = p.Name.ToLowerInvariant();
                // "deveTrocarSenha" é só um indicador (true ou false), não carrega senha.
                // "senhaTemporaria" é a senha gerada para o ADMIN repassar: aparece só nessa resposta e nunca é gravada.
                var permitido = nome == "devetrocarsenha" || (tipo == typeof(SenhaTemporariaResponse) && nome == "senhatemporaria");
                if (bloquearSenha && !permitido && (nome.Contains("senha") || nome.Contains("hash") || nome.Contains("password")))
                    problemas.Add($"{origem}: {tipo.Name}.{p.Name} não pode ser exposto");
                Inspecionar(p.PropertyType, origem, visitados, problemas, bloquearSenha);
            }
        }

        private static Type DesembrulharRetorno(Type retorno)
        {
            while (retorno.IsGenericType &&
                   (retorno.GetGenericTypeDefinition() == typeof(Task<>) ||
                    retorno.GetGenericTypeDefinition() == typeof(ActionResult<>)))
                retorno = retorno.GetGenericArguments()[0];
            return retorno;
        }

        [Fact]
        public void Existem_endpoints_para_inspecionar()
        {
            Assert.True(Acoes().Count() >= 15);
        }

        [Fact]
        public void Nenhum_endpoint_devolve_entidade_nem_senha()
        {
            var problemas = new List<string>();
            foreach (var acao in Acoes())
                Inspecionar(DesembrulharRetorno(acao.ReturnType), $"{acao.DeclaringType!.Name}.{acao.Name}", new HashSet<Type>(), problemas, bloquearSenha: true);

            Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
        }

        [Fact]
        public void Nenhum_endpoint_recebe_entidade_como_parametro()
        {
            var problemas = new List<string>();
            foreach (var acao in Acoes())
                foreach (var parametro in acao.GetParameters())
                    Inspecionar(parametro.ParameterType, $"{acao.DeclaringType!.Name}.{acao.Name}({parametro.Name})", new HashSet<Type>(), problemas, bloquearSenha: false);   // o login precisa receber a senha

            Assert.True(problemas.Count == 0, string.Join(Environment.NewLine, problemas));
        }
    }
}
