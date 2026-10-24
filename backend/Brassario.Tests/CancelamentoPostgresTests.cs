using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>
    /// API-27 no PostgreSQL de verdade (aplica a migration AddCancelamentoBatelada): motivo e data gravados, estorno nos dois lotes,
    /// detalhe da cancelada, custo zero na lista, consumo do dashboard sem a produção cancelada e o CHECK das novas colunas.
    /// </summary>
    [Trait("Categoria", "Docker")]
    public class CancelamentoPostgresTests : IClassFixture<ConcorrenciaPostgresFixture>
    {
        private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");

        private readonly ConcorrenciaPostgresFixture _banco;

        public CancelamentoPostgresTests(ConcorrenciaPostgresFixture banco) => _banco = banco;

        private class RelogioReal : IRelogio
        {
            public DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);
            public DateTime AgoraUtc => DateTime.UtcNow;
        }

        private class ReceitaDoBanco : IReceitaService
        {
            private readonly AppDbContext _db;
            public ReceitaDoBanco(AppDbContext db) => _db = db;

            public Task<Receita> ObterParaProducaoAsync(long id, CancellationToken ct = default) =>
                _db.Receitas.AsNoTracking().Include(r => r.Itens).ThenInclude(i => i.Insumo).SingleAsync(r => r.Id == id, ct);

            public Task<List<ReceitaResumoResponse>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> ObterAsync(long id, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> CriarAsync(ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AtualizarAsync(long id, ReceitaRequest request, CancellationToken ct = default) => throw new NotSupportedException();
            public Task<ReceitaResponse> AlterarStatusAsync(long id, bool ativa, CancellationToken ct = default) => throw new NotSupportedException();
        }

        private static BateladaService NovoServico(AppDbContext db) =>
            new(new ReceitaDoBanco(db), new BateladaRepository(db), new RelogioReal());

        private sealed record Base(long ReceitaId, long UsuarioId, long InsumoId, long LoteCedoId, long LoteTardeId);

        // Malte em dois lotes (10 kg que vencem antes e 40 kg que vencem depois) e uma receita de 18 kg por 100 L
        private async Task<Base> PrepararAsync()
        {
            await using var db = _banco.NovoContexto();
            await db.Database.ExecuteSqlRawAsync("TRUNCATE usuario, fornecedor, insumo, receita RESTART IDENTITY CASCADE");
            var hoje = new RelogioReal().Hoje;

            var malte = new Insumo
            {
                Nome = "Malte Pilsen", Categoria = CategoriaInsumo.MALTE, UnidadeMedida = UnidadeMedida.KG,
                EstoqueMinimo = 0, DiasValidadeCritica = 15, DiasValidadeAtencao = 45, Ativo = true
            };
            var receita = new Receita { Nome = "IPA Cascade", VolumeBaseLitros = 100, Ativa = true };
            receita.Itens.Add(new ReceitaItem { Insumo = malte, Quantidade = 18m });
            var usuario = new Usuario { Nome = "Ana Souza", Email = "ana@teste.local", SenhaHash = "hash", Perfil = PerfilUsuario.OPERADOR };
            var cedo = new LoteInsumo
            {
                Insumo = malte, CodigoLote = "M-CEDO", DataEntrada = hoje.AddDays(-60), DataValidade = hoje.AddDays(30),
                QuantidadeInicial = 10m, QuantidadeAtual = 10m, PrecoUnitario = 10m
            };
            var tarde = new LoteInsumo
            {
                Insumo = malte, CodigoLote = "M-TARDE", DataEntrada = hoje.AddDays(-30), DataValidade = hoje.AddDays(300),
                QuantidadeInicial = 40m, QuantidadeAtual = 40m, PrecoUnitario = 10m
            };
            db.AddRange(receita, usuario, cedo, tarde);
            await db.SaveChangesAsync();
            return new Base(receita.Id, usuario.Id, malte.Id, cedo.Id, tarde.Id);
        }

        private async Task<BateladaResponse> ProduzirAsync(Base b, decimal litros = 100m)
        {
            await using var db = _banco.NovoContexto();
            return await NovoServico(db).ProduzirAsync(new BateladaRequest { ReceitaId = b.ReceitaId, VolumePlanejado = litros }, b.UsuarioId);
        }

        private async Task<BateladaResponse> CancelarAsync(Base b, long id, string motivo)
        {
            await using var db = _banco.NovoContexto();
            return await NovoServico(db).CancelarAsync(id, new CancelarBateladaRequest { Motivo = motivo }, b.UsuarioId);
        }

        [Fact]
        public async Task Cancelar_grava_motivo_e_data_e_devolve_o_saldo_aos_dois_lotes_de_origem()
        {
            var b = await PrepararAsync();
            var producao = await ProduzirAsync(b);
            Assert.Equal(2, producao.Consumos.Count);    // 10 kg do lote que vence antes e 8 kg do outro

            var antes = DateTime.UtcNow.AddSeconds(-5);
            var r = await CancelarAsync(b, producao.Id, "Lote contaminado");

            Assert.Equal((StatusBatelada.CANCELADA, "Lote contaminado", 0m), (r.Status, r.MotivoCancelamento, r.CustoTotal));
            Assert.Equal(2, r.Estornos!.Count);

            await using var leitura = _banco.NovoContexto();
            var batelada = await leitura.Bateladas.AsNoTracking().SingleAsync();
            Assert.Equal((StatusBatelada.CANCELADA, "Lote contaminado"), (batelada.Status, batelada.MotivoCancelamento));
            Assert.InRange(batelada.CanceladoEm!.Value, antes, DateTime.UtcNow.AddSeconds(5));
            Assert.True(batelada.CustoTotal > 0);    // o custo original continua gravado; a resposta é que mostra zero

            var lotes = await leitura.LotesInsumo.AsNoTracking().OrderBy(l => l.Id).ToListAsync();
            Assert.Equal(new[] { (10m, 2L), (40m, 2L) }, lotes.Select(l => (l.QuantidadeAtual, l.Versao)));    // baixa e estorno: 2 versões

            var estornos = await leitura.Movimentacoes.AsNoTracking().Where(m => m.Tipo == TipoMovimentacao.ESTORNO).OrderBy(m => m.LoteInsumoId).ToListAsync();
            Assert.Equal(new[] { 10m, 8m }, estornos.Select(m => m.Quantidade));
            Assert.All(estornos, m => Assert.Equal(("Lote contaminado", batelada.Id), (m.Motivo, m.BateladaId)));
        }

        [Fact]
        public async Task Detalhe_da_cancelada_devolve_motivo_data_e_estornos_pelo_repositorio_real()
        {
            var b = await PrepararAsync();
            var producao = await ProduzirAsync(b);
            await CancelarAsync(b, producao.Id, "Lote contaminado");

            await using var db = _banco.NovoContexto();
            var d = await NovoServico(db).ObterAsync(producao.Id);

            Assert.Equal((StatusBatelada.CANCELADA, "Lote contaminado", 0m, "IPA Cascade"), (d.Status, d.MotivoCancelamento, d.CustoTotal, d.Receita));
            Assert.NotNull(d.CanceladoEm);
            Assert.Equal(new[] { ("M-CEDO", 10m), ("M-TARDE", 8m) }, d.Estornos!.Select(e => (e.CodigoLote, e.Quantidade)));
        }

        [Fact]
        public async Task Cancelar_duas_vezes_gera_422_e_o_estoque_nao_passa_do_original()
        {
            var b = await PrepararAsync();
            var producao = await ProduzirAsync(b);
            await CancelarAsync(b, producao.Id, "Primeiro cancelamento");

            var ex = await Assert.ThrowsAsync<NegocioException>(() => CancelarAsync(b, producao.Id, "Segundo cancelamento"));

            Assert.Equal("STATUS_INVALIDO", ex.Codigo);
            await using var leitura = _banco.NovoContexto();
            Assert.Equal(new[] { 10m, 40m }, (await leitura.LotesInsumo.AsNoTracking().OrderBy(l => l.Id).ToListAsync()).Select(l => l.QuantidadeAtual));
            Assert.Equal("Primeiro cancelamento", (await leitura.Bateladas.AsNoTracking().SingleAsync()).MotivoCancelamento);
        }

        [Fact]
        public async Task Na_lista_a_cancelada_aparece_com_custo_zero()
        {
            var b = await PrepararAsync();
            var cancelada = await ProduzirAsync(b);
            await CancelarAsync(b, cancelada.Id, "Lote contaminado");
            await ProduzirAsync(b, 50m);    // outra produção, que cabe no estoque devolvido

            await using var db = _banco.NovoContexto();
            var pagina = await new HistoricoService(new HistoricoRepository(db), Brasilia).ListarBateladasAsync(null, null, null, null, null, null);

            var linhaCancelada = Assert.Single(pagina.Itens, i => i.Status == StatusBatelada.CANCELADA);
            var linhaAtiva = Assert.Single(pagina.Itens, i => i.Status == StatusBatelada.EM_ANDAMENTO);
            Assert.Equal(0m, linhaCancelada.CustoTotal);
            Assert.True(linhaAtiva.CustoTotal > 0);
        }

        [Fact]
        public async Task Consumo_do_dashboard_nao_conta_a_producao_cancelada()
        {
            var b = await PrepararAsync();
            var cancelada = await ProduzirAsync(b);
            await CancelarAsync(b, cancelada.Id, "Lote contaminado");
            await ProduzirAsync(b);    // 18 kg que realmente foram consumidos

            await using var db = _banco.NovoContexto();
            var consumo = await new DashboardRepository(db).ListarConsumoDesdeAsync(
                new[] { b.InsumoId }, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddMinutes(5));

            Assert.Equal(18m, Assert.Single(consumo).Quantidade);
        }

        [Fact]
        public async Task Check_do_banco_nao_deixa_gravar_motivo_em_producao_que_nao_foi_cancelada()
        {
            var b = await PrepararAsync();
            await ProduzirAsync(b);

            await using var db = _banco.NovoContexto();
            var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
                db.Database.ExecuteSqlRawAsync("UPDATE batelada SET motivo_cancelamento = 'Motivo solto' WHERE status = 'EM_ANDAMENTO'"));
            Assert.Contains("ck_batelada_cancelamento", ex.ToString());

            await using var db2 = _banco.NovoContexto();
            await Assert.ThrowsAnyAsync<Exception>(() =>
                db2.Database.ExecuteSqlRawAsync("UPDATE batelada SET cancelado_em = now() WHERE status = 'EM_ANDAMENTO'"));
        }
    }
}
