using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class ReceitaRepository : IReceitaRepository
    {
        private readonly AppDbContext _db;

        public ReceitaRepository(AppDbContext db) => _db = db;

        public async Task<List<Receita>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default)
        {
            var consulta = _db.Receitas.AsNoTracking().Include(r => r.Itens).AsQueryable();

            if (!incluirInativas)
                consulta = consulta.Where(r => r.Ativa);

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLower();
                consulta = consulta.Where(r =>
                    r.Nome.ToLower().Contains(termo) ||
                    (r.Estilo != null && r.Estilo.ToLower().Contains(termo)));
            }

            return await consulta.OrderBy(r => r.Nome).ToListAsync(ct);
        }

        public Task<Receita?> ObterPorIdAsync(long id, CancellationToken ct = default) =>
            _db.Receitas
                .Include(r => r.Itens)
                .ThenInclude(i => i.Insumo)
                .FirstOrDefaultAsync(r => r.Id == id, ct);

        public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default)
        {
            var alvo = nome.Trim().ToLower();
            return _db.Receitas.AnyAsync(r => r.Nome.ToLower() == alvo && (ignorarId == null || r.Id != ignorarId), ct);
        }

        public Task<List<Insumo>> ObterInsumosAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default) =>
            _db.Insumos.Where(i => ids.Contains(i.Id)).ToListAsync(ct);

        public async Task AdicionarAsync(Receita receita, CancellationToken ct = default) =>
            await _db.Receitas.AddAsync(receita, ct);

        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
