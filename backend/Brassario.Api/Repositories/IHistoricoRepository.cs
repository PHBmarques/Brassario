using Brassario.Api.Models;

namespace Brassario.Api.Repositories
{
    /// <summary>Período em UTC: início inclusivo e fim exclusivo (o dia seguinte à meia-noite local).</summary>
    public sealed record FiltroBateladas(DateTime? InicioUtc, DateTime? FimUtcExclusivo, long? ReceitaId, StatusBatelada? Status);

    public sealed record FiltroMovimentacoes(long? InsumoId, long? LoteId, long? ProducaoId);

    /// <summary>Consultas de histórico (somente leitura), sempre paginadas e dos mais recentes para os mais antigos.</summary>
    public interface IHistoricoRepository
    {
        /// <summary>Produções com receita e responsável carregados, sem os consumos. Devolve a página e o total geral do filtro.</summary>
        Task<(List<Batelada> Itens, int Total)> ListarBateladasAsync(FiltroBateladas filtro, int pagina, int tamanho, CancellationToken ct = default);

        /// <summary>Movimentações com lote, insumo, produção e responsável carregados. Devolve a página e o total geral do filtro.</summary>
        Task<(List<MovimentacaoEstoque> Itens, int Total)> ListarMovimentacoesAsync(FiltroMovimentacoes filtro, int pagina, int tamanho, CancellationToken ct = default);
    }
}
