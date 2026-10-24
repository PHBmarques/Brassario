using Brassario.Api.Data;
using Brassario.Api.DTOs.Request;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class MovimentacaoRepository : IMovimentacaoRepository
    {
        private readonly AppDbContext _db;

        public MovimentacaoRepository(AppDbContext db) => _db = db;

        public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> operacao, CancellationToken ct = default)
        {
            await using var transacao = await _db.Database.BeginTransactionAsync(ct);
            var resultado = await operacao();
            await transacao.CommitAsync(ct);
            return resultado;
        }

        public async Task<LoteInsumo?> BloquearLoteAsync(long id, CancellationToken ct = default)
        {
            var lote = await _db.LotesInsumo
                .FromSqlInterpolated($"SELECT * FROM lote_insumo WHERE id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (lote is not null)
                await _db.Entry(lote).Reference(l => l.Insumo).LoadAsync(ct);
            return lote;
        }

        public async Task AdicionarAsync(MovimentacaoEstoque movimentacao, CancellationToken ct = default) =>
            await _db.Movimentacoes.AddAsync(movimentacao, ct);

        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

        public Task<List<MovimentacaoEstoque>> ListarAsync(MovimentacaoFiltro filtro, CancellationToken ct = default)
        {
            var consulta = _db.Movimentacoes.AsNoTracking()
                .Include(m => m.LoteInsumo).ThenInclude(l => l.Insumo)
                .AsQueryable();

            if (filtro.Tipo.HasValue) consulta = consulta.Where(m => m.Tipo == filtro.Tipo.Value);
            if (filtro.LoteId.HasValue) consulta = consulta.Where(m => m.LoteInsumoId == filtro.LoteId.Value);
            if (filtro.InsumoId.HasValue) consulta = consulta.Where(m => m.LoteInsumo.InsumoId == filtro.InsumoId.Value);
            if (filtro.UsuarioId.HasValue) consulta = consulta.Where(m => m.UsuarioId == filtro.UsuarioId.Value);
            if (filtro.BateladaId.HasValue) consulta = consulta.Where(m => m.BateladaId == filtro.BateladaId.Value);
            if (filtro.DataInicio.HasValue)
            {
                var inicio = filtro.DataInicio.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                consulta = consulta.Where(m => m.DataHora >= inicio);
            }
            if (filtro.DataFim.HasValue)
            {
                var fimExclusivo = filtro.DataFim.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                consulta = consulta.Where(m => m.DataHora < fimExclusivo);
            }

            return consulta.OrderByDescending(m => m.DataHora).ThenByDescending(m => m.Id).ToListAsync(ct);
        }
    }
}
