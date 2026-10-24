using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _db;

        public DashboardRepository(AppDbContext db) => _db = db;

        public Task<List<Insumo>> ListarInsumosAtivosAsync(CancellationToken ct = default) =>
            _db.Insumos.AsNoTracking().Where(i => i.Ativo).OrderBy(i => i.Nome).ToListAsync(ct);

        public Task<List<LoteInsumo>> ListarLotesAsync(IReadOnlyCollection<long> insumoIds, CancellationToken ct = default)
        {
            var ids = insumoIds.ToArray();
            if (ids.Length == 0) return Task.FromResult(new List<LoteInsumo>());
            return _db.LotesInsumo.AsNoTracking().Where(l => ids.Contains(l.InsumoId)).ToListAsync(ct);
        }

        public Task<List<ConsumoPorInsumo>> ListarConsumoDesdeAsync(
            IReadOnlyCollection<long> insumoIds, DateTime inicioUtc, DateTime fimUtc, CancellationToken ct = default)
        {
            var ids = insumoIds.ToArray();
            if (ids.Length == 0) return Task.FromResult(new List<ConsumoPorInsumo>());

            return _db.Movimentacoes.AsNoTracking()
                // Produção cancelada devolveu o estoque: o que ela consumiu não conta no consumo
                .Where(m => m.Tipo == TipoMovimentacao.SAIDA_PRODUCAO && m.DataHora >= inicioUtc && m.DataHora <= fimUtc
                    && (m.Batelada == null || m.Batelada.Status != StatusBatelada.CANCELADA)
                    && ids.Contains(m.LoteInsumo.InsumoId))
                .GroupBy(m => m.LoteInsumo.InsumoId)
                .Select(g => new ConsumoPorInsumo(g.Key,
                    g.Sum(m => m.Quantidade < 0 ? -m.Quantidade : m.Quantidade)))
                .ToListAsync(ct);
        }
    }
}
