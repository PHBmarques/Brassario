using Brassario.Api.Data;
using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Brassario.Api.Repositories
{
    public class HistoricoRepository : IHistoricoRepository
    {
        private readonly AppDbContext _db;

        public HistoricoRepository(AppDbContext db) => _db = db;

        public async Task<(List<Batelada> Itens, int Total)> ListarBateladasAsync(
            FiltroBateladas filtro, int pagina, int tamanho, CancellationToken ct = default)
        {
            var consulta = _db.Bateladas.AsNoTracking().AsQueryable();

            if (filtro.InicioUtc is { } inicio) consulta = consulta.Where(b => b.DataProducao >= inicio);
            if (filtro.FimUtcExclusivo is { } fim) consulta = consulta.Where(b => b.DataProducao < fim);
            if (filtro.ReceitaId is { } receitaId) consulta = consulta.Where(b => b.ReceitaId == receitaId);
            if (filtro.Status is { } status) consulta = consulta.Where(b => b.Status == status);

            var total = await consulta.CountAsync(ct);

            // Mais recentes primeiro; o id desempata produções no mesmo instante, para a paginação não repetir nem pular linhas
            var itens = await consulta
                .Include(b => b.Receita)
                .Include(b => b.Usuario)
                .OrderByDescending(b => b.DataProducao).ThenByDescending(b => b.Id)
                .Skip((pagina - 1) * tamanho).Take(tamanho)
                .ToListAsync(ct);

            return (itens, total);
        }

        public async Task<(List<MovimentacaoEstoque> Itens, int Total)> ListarMovimentacoesAsync(
            FiltroMovimentacoes filtro, int pagina, int tamanho, CancellationToken ct = default)
        {
            var consulta = _db.Movimentacoes.AsNoTracking().AsQueryable();

            if (filtro.InsumoId is { } insumoId) consulta = consulta.Where(m => m.LoteInsumo.InsumoId == insumoId);
            if (filtro.LoteId is { } loteId) consulta = consulta.Where(m => m.LoteInsumoId == loteId);
            if (filtro.ProducaoId is { } producaoId) consulta = consulta.Where(m => m.BateladaId == producaoId);

            var total = await consulta.CountAsync(ct);

            var itens = await consulta
                .Include(m => m.LoteInsumo).ThenInclude(l => l.Insumo)
                .Include(m => m.Usuario)
                .Include(m => m.Batelada)
                .OrderByDescending(m => m.DataHora).ThenByDescending(m => m.Id)
                .Skip((pagina - 1) * tamanho).Take(tamanho)
                .ToListAsync(ct);

            return (itens, total);
        }
    }
}
