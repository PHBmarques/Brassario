using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Middleware;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-16: o índice único (insumo_id, codigo_lote) e a geração de código, contra PostgreSQL real.</summary>
    [Trait("Categoria", "Docker")]
    public class LotesPostgresTests : IClassFixture<ConcorrenciaPostgresFixture>
    {
        private readonly ConcorrenciaPostgresFixture _banco;

        public LotesPostgresTests(ConcorrenciaPostgresFixture banco) => _banco = banco;

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => new(2026, 10, 19);
            public DateTime AgoraUtc => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        }

        private async Task<(long Malte, long Lupulo, long Usuario)> PrepararAsync()
        {
            await using var db = _banco.NovoContexto();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE usuario, fornecedor, insumo RESTART IDENTITY CASCADE");

            var malte = new Insumo { Nome = "Malte Pilsen", Categoria = CategoriaInsumo.MALTE, UnidadeMedida = UnidadeMedida.KG, Ativo = true };
            var lupulo = new Insumo { Nome = "Cascade", Categoria = CategoriaInsumo.LUPULO, UnidadeMedida = UnidadeMedida.KG, Ativo = true };
            var usuario = new Usuario { Nome = "Operador", Email = "op@teste.local", SenhaHash = "x", Perfil = PerfilUsuario.OPERADOR };
            db.AddRange(malte, lupulo, usuario);
            await db.SaveChangesAsync();
            return (malte.Id, lupulo.Id, usuario.Id);
        }

        private static LoteRequest Pedido(long insumoId, string? codigo) => new()
        {
            InsumoId = insumoId,
            CodigoLote = codigo,
            DataValidade = new DateOnly(2027, 3, 1),
            Quantidade = 10,
            PrecoUnitario = 5m
        };

        private LoteService NovoServico(Brassario.Api.Data.AppDbContext db) =>
            new(new LoteRepository(db), new InsumoRepository(db), new FornecedorRepository(db), new RelogioFixo());

        private static async Task<(int Status, JsonElement Corpo)> ComoApiResponderia(Exception ex)
        {
            var contexto = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
            contexto.Response.Body = new MemoryStream();
            await new ExceptionHandlingMiddleware(_ => throw ex, NullLogger<ExceptionHandlingMiddleware>.Instance).InvokeAsync(contexto);
            contexto.Response.Body.Position = 0;
            var texto = await new StreamReader(contexto.Response.Body).ReadToEndAsync();
            return (contexto.Response.StatusCode, JsonDocument.Parse(texto).RootElement);
        }

        [Fact]
        public async Task O_indice_unico_barra_o_duplicado_mesmo_sem_passar_pela_checagem_da_aplicacao()
        {
            var (malte, _, usuario) = await PrepararAsync();
            await using (var db = _banco.NovoContexto())
                await NovoServico(db).RegistrarEntradaAsync(Pedido(malte, "L-001"), usuario);

            // Insere direto no repositório, pulando o ExisteCodigoAsync: é o que acontece numa corrida entre duas entradas
            await using var db2 = _banco.NovoContexto();
            var repo = new LoteRepository(db2);
            await repo.AdicionarAsync(new LoteInsumo
            {
                InsumoId = malte,
                CodigoLote = "L-001",
                DataEntrada = new DateOnly(2026, 10, 19),
                DataValidade = new DateOnly(2027, 3, 1),
                QuantidadeInicial = 5,
                QuantidadeAtual = 5,
                PrecoUnitario = 1m
            });

            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => repo.SalvarAsync());

            var (status, corpo) = await ComoApiResponderia(ex);
            Assert.Equal(409, status);
            Assert.Equal("CODIGO_LOTE_DUPLICADO", corpo.GetProperty("codigo").GetString());
            Assert.Contains("Já existe um lote com esse código", corpo.GetProperty("mensagem").GetString());
        }

        [Fact]
        public async Task Duplicado_pelo_servico_devolve_CODIGO_LOTE_DUPLICADO_e_so_o_primeiro_fica_no_banco()
        {
            var (malte, _, usuario) = await PrepararAsync();
            await using (var db = _banco.NovoContexto())
                await NovoServico(db).RegistrarEntradaAsync(Pedido(malte, "ab-1"), usuario);

            await using var db2 = _banco.NovoContexto();
            var ex = await Assert.ThrowsAsync<ConflitoException>(
                () => NovoServico(db2).RegistrarEntradaAsync(Pedido(malte, " AB-1 "), usuario));

            Assert.Equal("CODIGO_LOTE_DUPLICADO", ex.Codigo);
            await using var conferencia = _banco.NovoContexto();
            var lote = await conferencia.LotesInsumo.SingleAsync();
            Assert.Equal("AB-1", lote.CodigoLote);   // gravado em maiúsculas
        }

        [Fact]
        public async Task Mesmo_codigo_em_insumos_diferentes_e_permitido_no_banco()
        {
            var (malte, lupulo, usuario) = await PrepararAsync();

            await using var db = _banco.NovoContexto();
            await NovoServico(db).RegistrarEntradaAsync(Pedido(malte, "L-001"), usuario);
            await NovoServico(db).RegistrarEntradaAsync(Pedido(lupulo, "L-001"), usuario);

            await using var conferencia = _banco.NovoContexto();
            Assert.Equal(2, await conferencia.LotesInsumo.CountAsync(l => l.CodigoLote == "L-001"));
        }

        [Fact]
        public async Task Codigo_vazio_gera_a_sequencia_L_AAAAMMDD_NN_no_banco_real()
        {
            var (malte, lupulo, usuario) = await PrepararAsync();

            var codigos = new List<string>();
            foreach (var insumo in new[] { malte, malte, malte, lupulo })
            {
                await using var db = _banco.NovoContexto();
                codigos.Add((await NovoServico(db).RegistrarEntradaAsync(Pedido(insumo, ""), usuario)).CodigoLote);
            }

            Assert.Equal(new[] { "L-20261019-01", "L-20261019-02", "L-20261019-03", "L-20261019-01" }, codigos);
        }
    }
}
