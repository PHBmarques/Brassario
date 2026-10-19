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
            var alvo = codigoLote.Trim().ToLower();
            return _db.LotesInsumo.AnyAsync(l => l.InsumoId == insumoId && l.CodigoLote.ToLower() == alvo, ct);
        }

        public async Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default) =>
            await _db.LotesInsumo.AddAsync(lote, ct);

        // O SaveChanges grava o lote e a movimentação ligada a ele em UMA transação: ou grava os dois, ou nenhum
        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
