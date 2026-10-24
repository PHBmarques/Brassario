using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class HistoricoService : IHistoricoService
    {
        public const int TamanhoPadrao = 20;
        public const int TamanhoMaximo = 100;
        private const int MaxPagina = 1_000_000;   // evita estouro do cálculo de Skip

        private readonly IHistoricoRepository _repo;
        private readonly TimeZoneInfo _fuso;

        // No Docker o servidor roda em America/Sao_Paulo; os testes passam um fuso fixo no outro construtor
        public HistoricoService(IHistoricoRepository repo) : this(repo, TimeZoneInfo.Local) { }

        public HistoricoService(IHistoricoRepository repo, TimeZoneInfo fuso)
        {
            _repo = repo;
            _fuso = fuso;
        }

        public async Task<PaginaResponse<BateladaResumoResponse>> ListarBateladasAsync(
            DateOnly? de, DateOnly? ate, long? receitaId, string? status, int? pagina, int? tamanho, CancellationToken ct = default)
        {
            var erros = new Dictionary<string, string[]>();
            var (p, t) = ValidarPagina(pagina, tamanho, erros);

            if (de is not null && ate is not null && de > ate)
                erros["de"] = new[] { "A data inicial não pode ser depois da data final." };

            if (receitaId is <= 0)
                erros["receitaId"] = new[] { "Informe um id de receita válido." };

            StatusBatelada? statusFiltro = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<StatusBatelada>(status.Trim(), true, out var s) && Enum.IsDefined(s))
                    statusFiltro = s;
                else
                    erros["status"] = new[] { $"Status inválido. Use: {string.Join(", ", Enum.GetNames<StatusBatelada>())}." };
            }

            if (erros.Count > 0) throw new ValidacaoException(erros);

            var (inicio, fim) = PeriodoLocal.ParaUtc(de, ate, _fuso);
            var (itens, total) = await _repo.ListarBateladasAsync(new FiltroBateladas(inicio, fim, receitaId, statusFiltro), p, t, ct);

            var linhas = itens.Select(b => new BateladaResumoResponse(
                b.Id, b.Codigo, b.ReceitaId, b.Receita.Nome, b.DataProducao,
                b.VolumeFinal ?? b.VolumePlanejado, b.Status == StatusBatelada.CANCELADA ? 0m : b.CustoTotal,
                b.UsuarioId, b.Usuario.Nome, b.Status)).ToList();

            return PaginaResponse<BateladaResumoResponse>.Criar(linhas, p, t, total);
        }

        public async Task<PaginaResponse<MovimentacaoResponse>> ListarMovimentacoesAsync(
            long? insumoId, long? loteId, long? producaoId, int? pagina, int? tamanho, CancellationToken ct = default)
        {
            var erros = new Dictionary<string, string[]>();
            var (p, t) = ValidarPagina(pagina, tamanho, erros);

            if (insumoId is <= 0) erros["insumoId"] = new[] { "Informe um id de insumo válido." };
            if (loteId is <= 0) erros["loteId"] = new[] { "Informe um id de lote válido." };
            if (producaoId is <= 0) erros["producaoId"] = new[] { "Informe um id de produção válido." };

            if (erros.Count > 0) throw new ValidacaoException(erros);

            var (itens, total) = await _repo.ListarMovimentacoesAsync(new FiltroMovimentacoes(insumoId, loteId, producaoId), p, t, ct);

            var linhas = itens.Select(m => new MovimentacaoResponse(
                m.Id, m.DataHora, m.Tipo, m.Quantidade, m.Motivo,
                m.LoteInsumo.InsumoId, m.LoteInsumo.Insumo.Nome, m.LoteInsumo.Insumo.UnidadeMedida,
                m.LoteInsumoId, m.LoteInsumo.CodigoLote,
                m.BateladaId, m.Batelada?.Codigo,
                m.UsuarioId, m.Usuario.Nome)).ToList();

            return PaginaResponse<MovimentacaoResponse>.Criar(linhas, p, t, total);
        }

        private static (int Pagina, int Tamanho) ValidarPagina(int? pagina, int? tamanho, Dictionary<string, string[]> erros)
        {
            var p = pagina ?? 1;
            var t = tamanho ?? TamanhoPadrao;

            if (p < 1 || p > MaxPagina) erros["pagina"] = new[] { $"A página vai de 1 a {MaxPagina}." };
            if (t < 1 || t > TamanhoMaximo) erros["tamanho"] = new[] { $"O tamanho da página vai de 1 a {TamanhoMaximo}." };

            return (p, t);
        }
    }
}
