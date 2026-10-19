using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class FornecedorRepository : IFornecedorRepository
    {
        private readonly AppDbContext _db;

        public FornecedorRepository(AppDbContext db) => _db = db;

        public Task<Fornecedor?> ObterPorIdAsync(long id, CancellationToken ct = default) =>
            _db.Fornecedores.FirstOrDefaultAsync(f => f.Id == id, ct);
    }
}
