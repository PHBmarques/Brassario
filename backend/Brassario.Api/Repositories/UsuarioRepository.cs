using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class UsuarioRepository : IUsuarioRepository
    {
        private readonly AppDbContext _db;

        public UsuarioRepository(AppDbContext db) => _db = db;

        public async Task<List<Usuario>> ListarAsync(string? busca, bool incluirInativos, CancellationToken ct = default)
        {
            var consulta = _db.Usuarios.AsNoTracking().AsQueryable();

            if (!incluirInativos)
                consulta = consulta.Where(u => u.Ativo);

            if (!string.IsNullOrWhiteSpace(busca))
            {
                var termo = busca.Trim().ToLower();
                consulta = consulta.Where(u => u.Nome.ToLower().Contains(termo) || u.Email.ToLower().Contains(termo));
            }

            return await consulta.OrderBy(u => u.Nome).ToListAsync(ct);
        }

        public Task<Usuario?> ObterPorIdAsync(long id, CancellationToken ct = default) =>
            _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);

        public Task<bool> ExisteEmailAsync(string email, long? ignorarId, CancellationToken ct = default)
        {
            var alvo = email.Trim().ToLower();
            return _db.Usuarios.AnyAsync(u => u.Email.ToLower() == alvo && (ignorarId == null || u.Id != ignorarId), ct);
        }

        public Task<int> ContarAdminsAtivosExcetoAsync(long usuarioId, CancellationToken ct = default) =>
            _db.Usuarios.CountAsync(u => u.Id != usuarioId && u.Ativo && u.Perfil == PerfilUsuario.ADMIN, ct);

        public async Task RevogarSessoesAsync(long usuarioId, CancellationToken ct = default)
        {
            var tokens = await _db.RefreshTokens.Where(t => t.UsuarioId == usuarioId && !t.Revogado).ToListAsync(ct);
            foreach (var token in tokens)
                token.Revogado = true;
        }

        public async Task AdicionarAsync(Usuario usuario, CancellationToken ct = default) =>
            await _db.Usuarios.AddAsync(usuario, ct);

        public Task SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }
}
