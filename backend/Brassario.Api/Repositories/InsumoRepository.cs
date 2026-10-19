using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class InsumoRepository : IInsumoRepository
    {
        private readonly AppDbContext _db;

        public InsumoRepository(AppDbContext db) => _db = db;

        public async Task<List<Insumo>> ListarAsync(
            CategoriaInsumo? categoria, string? busca, bool incluirInativos, CancellationToken ct = default)
        {
            var consulta = _db.Insumos.AsNoTracking().AsQueryable();

            if (!incluirInativos)
                consulta = consulta.Where(i => i.Ativo);

            if (categoria.HasValue)
                consulta = consulta.Where(i => i.Categoria == categoria.Value);

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLower();
                consulta = consulta.Where(i => i.Nome.ToLower().Contains(termo));
            }

            return await consulta.OrderBy(i => i.Nome).ToListAsync(ct);
        }

        public Task<Insumo?> ObterPorIdAsync(long id, CancellationToken ct = default) =>
            _db.Insumos.FirstOrDefaultAsync(i => i.Id == id, ct);

        public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default)
        {
            var alvo = nome.Trim().ToLower();
            return _db.Insumos.AnyAsync(i => i.Nome.ToLower() == alvo && (ignorarId == null || i.Id != ignorarId), ct);
        }

        public async Task AdicionarAsync(Insumo insumo, CancellationToken ct = default) =>
            await _db.Insumos.AddAsync(insumo, ct);

        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
