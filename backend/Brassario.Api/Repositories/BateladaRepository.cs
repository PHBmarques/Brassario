using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class BateladaRepository : IBateladaRepository
    {
        private readonly AppDbContext _db;

        public BateladaRepository(AppDbContext db) => _db = db;

        public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default)
        {
            // Sem Commit explícito, o Dispose da transação faz o rollback: qualquer exceção desfaz tudo
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);
            var resultado = await operacao();
            await transacao.CommitAsync(ct);
            return resultado;
        }

        public async Task<List<LoteInsumo>> BloquearLotesAsync(IReadOnlyCollection<long> insumoIds, CancellationToken ct = default)
        {
            var ids = insumoIds.ToArray();

            // A consulta não pode receber mais nenhum operador do LINQ: o FOR UPDATE precisa ficar no fim do SQL.
            // Os lotes voltam rastreados, então as alterações de saldo são gravadas no SalvarAsync.
            return await _db.LotesInsumo
                .FromSqlInterpolated($@"
                SELECT * FROM lote_insumo
                WHERE insumo_id = ANY({ids}) AND quantidade_atual > 0
                ORDER BY insumo_id, data_validade, data_entrada, id
                FOR UPDATE")
                .ToListAsync(ct);
        }

        public async Task<string> ProximoCodigoAsync(int ano, CancellationToken ct = default)
        {
            // Trava até o fim da transação: duas produções simultâneas não geram o mesmo código
            await _db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(8201)", ct);

            var prefixo = $"B-{ano}-";
            var ultimo = await _db.Bateladas
                .Where(b => b.Codigo.StartsWith(prefixo))
                .OrderByDescending(b => b.Codigo.Length).ThenByDescending(b => b.Codigo)
                .Select(b => b.Codigo)
                .FirstOrDefaultAsync(ct);

            var numero = 0;
            if (ultimo is not null)
                int.TryParse(ultimo[prefixo.Length..], out numero);

            return $"{prefixo}{numero + 1:0000}";
        }

        public async Task AdicionarAsync(Batelada batelada, CancellationToken ct = default) =>
            await _db.Bateladas.AddAsync(batelada, ct);

        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
