using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class FornecedorRepository : IFornecedorCadastroRepository
    {
        private readonly AppDbContext _db;

        public FornecedorRepository(AppDbContext db) => _db = db;

        public Task<Fornecedor?> ObterPorIdAsync(long id, CancellationToken ct = default) =>
            _db.Fornecedores.FirstOrDefaultAsync(f => f.Id == id, ct);

        public async Task<List<Fornecedor>> ListarAsync(string? busca, CancellationToken ct = default)
        {
            var consulta = _db.Fornecedores.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLower();
                consulta = consulta.Where(f => f.Nome.ToLower().Contains(termo));
            }

            return await consulta.OrderBy(f => f.Nome).ToListAsync(ct);
        }

        public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default)
        {
            var alvo = nome.Trim().ToLower();
            return _db.Fornecedores.AnyAsync(f => f.Nome.ToLower() == alvo && (ignorarId == null || f.Id != ignorarId), ct);
        }

        public async Task AdicionarAsync(Fornecedor fornecedor, CancellationToken ct = default) =>
            await _db.Fornecedores.AddAsync(fornecedor, ct);

        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
