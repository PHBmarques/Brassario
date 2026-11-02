using System.Text.Json;
using System.Text.Json.Serialization;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>Campo que não existe no DTO vira 400 VALIDACAO apontando o campo (e não é ignorado).</summary>
    public class CamposDesconhecidosTests
    {
        private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Converters = { new JsonStringEnumConverter() }
        };

        private static ErroResponse Responder(string json, Type tipo, string chaveModelState)
        {
            // Reproduz o que o formatter do MVC faz: guarda a exceção do System.Text.Json no ModelState
            var ex = Assert.ThrowsAny<JsonException>(() => JsonSerializer.Deserialize(json, tipo, Opcoes));
            var modelState = new ModelStateDictionary();
            var metadata = new EmptyModelMetadataProvider().GetMetadataForType(tipo);
            modelState.TryAddModelError(chaveModelState, new InputFormatterException(ex.Message, ex), metadata);

            var contexto = new ActionContext(new DefaultHttpContext(), new RouteData(),
                new ActionDescriptor { Parameters = new List<ParameterDescriptor> { new() { Name = "request" } } }, modelState);

            var resultado = Assert.IsType<BadRequestObjectResult>(ValidacaoResposta.Criar(contexto));
            return Assert.IsType<ErroResponse>(resultado.Value);
        }

        [Fact]
        public void Campo_sobrando_no_login_devolve_400_VALIDACAO_apontando_o_campo()
        {
            var erro = Responder("{\"email\":\"a@b.com\",\"senha\":\"x\",\"admin\":true}", typeof(LoginRequest), "$");

            Assert.Equal(400, erro.Status);
            Assert.Equal("VALIDACAO", erro.Codigo);
            Assert.Contains("desconhecido", Assert.Single(erro.Detalhes!["admin"]));
        }

        [Fact]
        public void Campo_sobrando_dentro_de_um_objeto_aninhado_aponta_o_caminho()
        {
            var erro = Responder("{\"lote\":{\"quantidade\":1,\"sobrando\":2}}", typeof(EntradaRequest), "$.lote");

            Assert.True(erro.Detalhes!.ContainsKey("lote.sobrando"));
        }

        [Fact]
        public void O_campo_manterConectado_do_login_continua_aceito()
        {
            var req = JsonSerializer.Deserialize<LoginRequest>("{\"email\":\"a@b.com\",\"senha\":\"x\",\"manterConectado\":true}", Opcoes);

            Assert.True(req!.ManterConectado);
        }

        [Fact]
        public void Maiusculas_e_minusculas_dos_campos_conhecidos_continuam_valendo()
        {
            var req = JsonSerializer.Deserialize<LoginRequest>("{\"Email\":\"a@b.com\",\"SENHA\":\"x\"}", Opcoes);

            Assert.Equal("a@b.com", req!.Email);
        }
    }
}
