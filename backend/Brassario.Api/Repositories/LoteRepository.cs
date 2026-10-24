using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class LoteRepository: ILoteRepository
    {
        private readonly AppDbContext _db;

        public LoteRepository(AppDbContext db) => _db = db;

        public Task<bool> ExisteCodigoAsync(long insumoId, string codigoLote, CancellationToken ct = default)
        {
            // Os códigos são gravados em maiúsculas; comparar por igualdade usa o índice único (insumo_id, codigo_lote)
            var alvo = codigoLote.Trim().ToUpperInvariant();
            return _db.LotesInsumo.AnyAsync(l => l.InsumoId == insumoId && l.CodigoLote == alvo, ct);
        }

        public async Task<int> ProximoSequencialAsync(long insumoId, string prefixo, CancellationToken ct = default)
        {
            var codigos = await _db.LotesInsumo
                .AsNoTracking()
                .Where(l => l.InsumoId == insumoId && l.CodigoLote.StartsWith(prefixo))
                .Select(l => l.CodigoLote)
                .ToListAsync(ct);

            var maior = 0;
            foreach (var codigo in codigos)
                if (int.TryParse(codigo.AsSpan(prefixo.Length), out var n) && n > maior)
                    maior = n;

            return maior + 1;
        }

        public async Task<List<LoteInsumo>> ListarPorInsumosAsync(
            IReadOnlyCollection<long> insumoIds, bool incluirEsgotados, CancellationToken ct = default)
        {
            var ids = insumoIds.ToArray();

            var consulta = _db.LotesInsumo
                .AsNoTracking()
                .Include(l => l.Fornecedor)
                .Where(l => ids.Contains(l.InsumoId));

            if (!incluirEsgotados)
                consulta = consulta.Where(l => l.QuantidadeAtual > 0);

            return await consulta.ToListAsync(ct);
        }

        public async Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default) =>
            await _db.LotesInsumo.AddAsync(lote, ct);

        // O SaveChanges grava o lote e a movimentação ligada a ele em UMA transação: ou grava os dois, ou nenhum
        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
