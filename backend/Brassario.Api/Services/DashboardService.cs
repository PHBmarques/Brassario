using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class DashboardService : IDashboardService
    {
        private const int DiasPeriodoConsumo = 30;
        private const decimal LimiteDiasCompra = 15m;

        private readonly IDashboardRepository _repo;
        private readonly IRelogio _relogio;

        public DashboardService(IDashboardRepository repo, IRelogio relogio)
        {
            _repo = repo;
            _relogio = relogio;
        }

        public async Task<DashboardResumoResponse> ObterResumoAsync(CancellationToken ct = default)
        {
            var dados = await ObterLinhasAsync(ct);
            var saldoTotal = dados.Linhas.Sum(x => x.SaldoUtilizavel);
            return new DashboardResumoResponse(
                dados.GeradoEmUtc,
                dados.InicioPeriodoUtc,
                dados.Linhas.Count,
                saldoTotal,
                dados.Linhas.Sum(x => x.Consumo30Dias),
                dados.Linhas.Count(x => x.SaldoUtilizavel <= 0),
                dados.Linhas.Count(x => x.SaldoUtilizavel < x.Insumo.EstoqueMinimo),
                dados.Linhas.Count(PrecisaComprar));
        }

        public async Task<List<DashboardConsumoResponse>> ListarConsumoAsync(CancellationToken ct = default)
        {
            var dados = await ObterLinhasAsync(ct);
            return dados.Linhas.Select(x => new DashboardConsumoResponse(
                x.Insumo.Id, x.Insumo.Nome, x.Insumo.Categoria, x.Insumo.UnidadeMedida,
                x.Consumo30Dias, x.MediaDiaria, x.SaldoUtilizavel))
                .OrderByDescending(x => x.ConsumoUltimos30Dias)
                .ThenBy(x => x.InsumoNome)
                .ToList();
        }

        public async Task<List<DashboardPrevisaoResponse>> ListarPrevisaoAsync(CancellationToken ct = default)
        {
            var dados = await ObterLinhasAsync(ct);
            return dados.Linhas.Select(x => new DashboardPrevisaoResponse(
                x.Insumo.Id, x.Insumo.Nome, x.Insumo.Categoria, x.Insumo.UnidadeMedida,
                x.SaldoUtilizavel, x.MediaDiaria, x.DiasRestantes, SugerirCompra(x.DiasRestantes)))
                .OrderBy(x => x.DiasRestantes ?? decimal.MaxValue)
                .ThenBy(x => x.InsumoNome)
                .ToList();
        }

        public async Task<List<SugestaoCompraResponse>> ListarSugestoesCompraAsync(CancellationToken ct = default)
        {
            var dados = await ObterLinhasAsync(ct);
            return dados.Linhas.Where(PrecisaComprar)
                .Select(x => new SugestaoCompraResponse(
                    x.Insumo.Id, x.Insumo.Nome, x.Insumo.Categoria, x.Insumo.UnidadeMedida,
                    x.SaldoUtilizavel, x.Insumo.EstoqueMinimo, x.MediaDiaria, x.DiasRestantes,
                    x.SaldoUtilizavel <= 0
                        ? "Sem estoque utilizável."
                        : SugerirCompra(x.DiasRestantes)
                            ? "O estoque deve durar 15 dias ou menos ao ritmo médio de consumo."
                            : "Saldo abaixo do estoque mínimo."))
                .OrderBy(x => x.DiasRestantes ?? decimal.MaxValue)
                .ThenBy(x => x.InsumoNome)
                .ToList();
        }

        private async Task<Snapshot> ObterLinhasAsync(CancellationToken ct)
        {
            var agora = _relogio.AgoraUtc;
            var inicio = agora.AddDays(-DiasPeriodoConsumo);
            var hoje = _relogio.Hoje;
            var insumos = await _repo.ListarInsumosAtivosAsync(ct);
            var ids = insumos.Select(i => i.Id).ToArray();
            var lotes = await _repo.ListarLotesAsync(ids, ct);
            var consumos = await _repo.ListarConsumoDesdeAsync(ids, inicio, agora, ct);
            var consumoPorInsumo = consumos.ToDictionary(x => x.InsumoId, x => x.Quantidade);
            var lotesPorInsumo = lotes.ToLookup(l => l.InsumoId);

            var linhas = insumos.Select(insumo =>
            {
                var saldo = lotesPorInsumo[insumo.Id]
                    .Where(l => l.QuantidadeAtual > 0 && l.DataValidade >= hoje)
                    .Sum(l => l.QuantidadeAtual);
                var totalConsumido = consumoPorInsumo.GetValueOrDefault(insumo.Id);
                var media = totalConsumido / DiasPeriodoConsumo;
                decimal? diasRestantes = saldo <= 0
                    ? 0m
                    : media > 0 ? saldo / media : null;
                return new Linha(insumo, saldo, totalConsumido, media, diasRestantes);
            }).ToList();

            return new Snapshot(agora, inicio, linhas);
        }

        private static bool SugerirCompra(decimal? diasRestantes) =>
            diasRestantes.HasValue && diasRestantes.Value <= LimiteDiasCompra;

        // Compra sugerida: acaba em até 15 dias OU o saldo está abaixo do estoque mínimo
        // (o mesmo critério do alerta "Estoque baixo", para a lista de compras não ficar vazia).
        private static bool PrecisaComprar(Linha x) =>
            SugerirCompra(x.DiasRestantes) || x.SaldoUtilizavel < x.Insumo.EstoqueMinimo;

        private sealed record Linha(Insumo Insumo, decimal SaldoUtilizavel, decimal Consumo30Dias,
            decimal MediaDiaria, decimal? DiasRestantes);
        private sealed record Snapshot(DateTime GeradoEmUtc, DateTime InicioPeriodoUtc, List<Linha> Linhas);
    }
}



