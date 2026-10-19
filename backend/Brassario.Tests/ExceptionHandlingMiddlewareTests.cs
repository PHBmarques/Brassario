using System.Text.Json;
using Brassario.Api.Exceptions;
using Brassario.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Brassario.Tests
{
    public class ExceptionHandlingMiddlewareTests
    {
        private static async Task<(int Status, JsonElement Corpo, string Texto)> Executar(Exception ex)
        {
            var contexto = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            contexto.Response.Body = new MemoryStream();

            var middleware = new ExceptionHandlingMiddleware(
                _ => throw ex, NullLogger<ExceptionHandlingMiddleware>.Instance);

            await middleware.InvokeAsync(contexto);

            contexto.Response.Body.Position = 0;
            var texto = await new StreamReader(contexto.Response.Body).ReadToEndAsync();
            return (contexto.Response.StatusCode, JsonDocument.Parse(texto).RootElement, texto);
        }

        [Fact]
        public async Task Nao_encontrado_vira_404()
        {
            var (status, corpo, _) = await Executar(new RecursoNaoEncontradoException("Insumo", 5));
            Assert.Equal(404, status);
            Assert.Equal("RECURSO_NAO_ENCONTRADO", corpo.GetProperty("codigo").GetString());
            Assert.Equal(404, corpo.GetProperty("status").GetInt32());
        }

        [Fact]
        public async Task Duplicidade_vira_409()
        {
            var (status, corpo, _) = await Executar(new ConflitoException("SKU repetido."));
            Assert.Equal(409, status);
            Assert.Equal("CONFLITO", corpo.GetProperty("codigo").GetString());
        }

        [Fact]
        public async Task Regra_de_negocio_vira_422()
        {
            var (status, corpo, _) = await Executar(new NegocioException("Regra violada."));
            Assert.Equal(422, status);
            Assert.Equal("REGRA_NEGOCIO", corpo.GetProperty("codigo").GetString());
        }

        [Fact]
        public async Task Estoque_insuficiente_vira_422_com_codigo_proprio()
        {
            var (status, corpo, _) = await Executar(new EstoqueInsuficienteException("Cascade", 2m, 1m));
            Assert.Equal(422, status);
            Assert.Equal("ESTOQUE_INSUFICIENTE", corpo.GetProperty("codigo").GetString());
        }

        [Fact]
        public async Task Validacao_vira_400_e_lista_o_campo()
        {
            var (status, corpo, _) = await Executar(new ValidacaoException("quantidade", "Deve ser maior que zero."));
            Assert.Equal(400, status);
            Assert.Equal("VALIDACAO", corpo.GetProperty("codigo").GetString());
            Assert.Equal("Deve ser maior que zero.",
                corpo.GetProperty("detalhes").GetProperty("quantidade")[0].GetString());
        }

        [Fact]
        public async Task Erro_inesperado_vira_500_sem_vazar_detalhes()
        {
            var (status, corpo, texto) = await Executar(new InvalidOperationException("segredo interno"));
            Assert.Equal(500, status);
            Assert.Equal("ERRO_INTERNO", corpo.GetProperty("codigo").GetString());
            Assert.DoesNotContain("segredo interno", texto);
            Assert.DoesNotContain("StackTrace", texto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" at ", texto);
        }

    }
}
