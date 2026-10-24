using Brassario.Api.DTOs.Response;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class AlertasService : IAlertasService
    {
        private readonly IInsumoRepository _insumos;
        private readonly ILoteRepository _lotes;
        private readonly IRelogio _relogio;

        public AlertasService(IInsumoRepository insumos, ILoteRepository lotes, IRelogio relogio)
        {
            _insumos = insumos;
            _lotes = lotes;
            _relogio = relogio;
        }

        public async Task<List<AlertaResponse>> ListarAsync(
            TipoAlerta? tipo, SeveridadeAlerta? severidade, CancellationToken ct = default)
        {
            var insumos = await _insumos.ListarAsync(null, null, false, ct);
            if (insumos.Count == 0) return new List<AlertaResponse>();

            var lotes = await _lotes.ListarPorInsumosAsync(insumos.Select(i => i.Id).ToArray(), true, ct);
            var porInsumo = lotes.ToLookup(l => l.InsumoId);
            var hoje = _relogio.Hoje;
            var alertas = new List<AlertaResponse>();

            foreach (var insumo in insumos)
            {
                var lotesDoInsumo = porInsumo[insumo.Id].ToList();
                foreach (var lote in lotesDoInsumo.Where(l => l.QuantidadeAtual > 0))
                {
                    var dias = SituacaoValidadeCalculador.DiasParaVencer(lote.DataValidade, hoje);
                    if (dias < 0)
                        alertas.Add(CriarAlerta(TipoAlerta.VENCIDO, SeveridadeAlerta.CRITICA, insumo, lote, dias,
                            lote.QuantidadeAtual, "O lote está vencido e ainda possui saldo."));
                    else if (dias <= insumo.DiasValidadeCritica)
                        alertas.Add(CriarAlerta(TipoAlerta.VALIDADE_CRITICA, SeveridadeAlerta.ALTA, insumo, lote, dias,
                            lote.QuantidadeAtual, "O lote está dentro do prazo de validade crítica."));
                    else if (dias <= insumo.DiasValidadeAtencao)
                        alertas.Add(CriarAlerta(TipoAlerta.ATENCAO, SeveridadeAlerta.MEDIA, insumo, lote, dias,
                            lote.QuantidadeAtual, "O lote está próximo do vencimento."));
                }

                var saldoUtilizavel = lotesDoInsumo
                    .Where(l => l.QuantidadeAtual > 0 && l.DataValidade >= hoje)
                    .Sum(l => l.QuantidadeAtual);

                if (saldoUtilizavel <= 0)
                    alertas.Add(CriarAlerta(TipoAlerta.ESTOQUE_VAZIO, SeveridadeAlerta.ALTA, insumo, null, null,
                        saldoUtilizavel, "Não há estoque utilizável para este insumo."));
                else if (saldoUtilizavel < insumo.EstoqueMinimo)
                    alertas.Add(CriarAlerta(TipoAlerta.ESTOQUE_BAIXO, SeveridadeAlerta.BAIXA, insumo, null, null,
                        saldoUtilizavel, "O estoque utilizável está abaixo do mínimo definido."));
            }

            return alertas
                .Where(a => !tipo.HasValue || a.Tipo == tipo.Value)
                .Where(a => !severidade.HasValue || a.Severidade == severidade.Value)
                .OrderByDescending(a => a.Severidade)
                .ThenBy(a => a.DiasParaVencer ?? int.MaxValue)
                .ThenBy(a => a.InsumoNome)
                .ThenBy(a => a.LoteId)
                .ToList();
        }

        private static AlertaResponse CriarAlerta(
            TipoAlerta tipo, SeveridadeAlerta severidade, Insumo insumo, LoteInsumo? lote,
            int? diasParaVencer, decimal saldo, string mensagem) => new(
                tipo, severidade, insumo.Id, insumo.Nome, insumo.Categoria, insumo.UnidadeMedida,
                lote?.Id, lote?.CodigoLote, lote?.DataValidade, diasParaVencer,
                saldo, insumo.EstoqueMinimo, mensagem);
    }
}
