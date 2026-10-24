using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class PerdaRepository : IPerdaRepository
    {
        private readonly AppDbContext _db;

        public PerdaRepository(AppDbContext db) => _db = db;

        public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default)
        {
            // Sem Commit explícito, o Dispose da transação faz o rollback
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);
            var resultado = await operacao();
            await transacao.CommitAsync(ct);
            return resultado;
        }

        public async Task<LoteInsumo?> BloquearLoteAsync(long loteId, CancellationToken ct = default)
        {
            // Sem operadores do LINQ depois do SQL: o FOR UPDATE precisa ficar no fim da consulta
            var lotes = await _db.LotesInsumo
                .FromSqlInterpolated($"SELECT * FROM lote_insumo WHERE id = {loteId} FOR UPDATE")
                .ToListAsync(ct);
            return lotes.FirstOrDefault();
        }

        public Task<Insumo?> ObterInsumoAsync(long insumoId, CancellationToken ct = default) =>
            _db.Insumos.AsNoTracking().FirstOrDefaultAsync(i => i.Id == insumoId, ct);

        public async Task AdicionarMovimentacaoAsync(MovimentacaoEstoque movimentacao, CancellationToken ct = default) =>
            await _db.Movimentacoes.AddAsync(movimentacao, ct);

        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
