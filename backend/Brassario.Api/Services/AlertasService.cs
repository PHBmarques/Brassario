using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class AlertasService : IAlertasService
    {
        public const int LimiteMaximo = 500;

        private readonly IInsumoRepository _insumos;
        private readonly ILoteRepository _lotes;
        private readonly IRelogio _relogio;

        public AlertasService(IInsumoRepository insumos, ILoteRepository lotes, IRelogio relogio)
        {
            _insumos = insumos;
            _lotes = lotes;
            _relogio = relogio;
        }

        /// <summary>
        /// ÚNICO lugar que decide a severidade e a ação de cada tipo de alerta:
        /// lote vencido gera REGISTRAR_PERDA; validade crítica ou em atenção gera VER_LOTES;
        /// saldo zero gera DAR_ENTRADA; abaixo do mínimo gera VER_COMPRAS.
        /// </summary>
        public static (SeveridadeAlerta Severidade, AcaoAlerta Acao) Regra(TipoAlerta tipo) => tipo switch
        {
            TipoAlerta.VENCIDO => (SeveridadeAlerta.CRITICA, AcaoAlerta.REGISTRAR_PERDA),
            TipoAlerta.VALIDADE_CRITICA => (SeveridadeAlerta.ALTA, AcaoAlerta.VER_LOTES),
            TipoAlerta.ATENCAO => (SeveridadeAlerta.MEDIA, AcaoAlerta.VER_LOTES),
            TipoAlerta.ESTOQUE_VAZIO => (SeveridadeAlerta.ALTA, AcaoAlerta.DAR_ENTRADA),
            TipoAlerta.ESTOQUE_BAIXO => (SeveridadeAlerta.BAIXA, AcaoAlerta.VER_COMPRAS),
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };

        public async Task<List<AlertaResponse>> ListarAsync(
            TipoAlerta? tipo, SeveridadeAlerta? severidade, int? limite = null, CancellationToken ct = default)
        {
            if (limite is < 1 or > LimiteMaximo)
                throw new ValidacaoException("limite", $"O limite vai de 1 a {LimiteMaximo}.");

            var insumos = await _insumos.ListarAsync(null, null, false, ct);
            if (insumos.Count == 0) return new List<AlertaResponse>();

            var lotes = await _lotes.ListarPorInsumosAsync(insumos.Select(i => i.Id).ToArray(), true, ct);
            var porInsumo = lotes.ToLookup(l => l.InsumoId);
            var hoje = _relogio.Hoje;
            var alertas = new List<AlertaResponse>();

            foreach (var insumo in insumos)
            {
                var lotesDoInsumo = porInsumo[insumo.Id].ToList();
                // Lote com saldo zero não gera alerta de validade
                foreach (var lote in lotesDoInsumo.Where(l => l.QuantidadeAtual > 0))
                {
                    var dias = SituacaoValidadeCalculador.DiasParaVencer(lote.DataValidade, hoje);
                    if (dias < 0)
                        alertas.Add(CriarAlerta(TipoAlerta.VENCIDO, insumo, lote, dias,
                            lote.QuantidadeAtual, "O lote está vencido e ainda possui saldo."));
                    else if (dias <= insumo.DiasValidadeCritica)
                        alertas.Add(CriarAlerta(TipoAlerta.VALIDADE_CRITICA, insumo, lote, dias,
                            lote.QuantidadeAtual, "O lote está dentro do prazo de validade crítica."));
                    else if (dias <= insumo.DiasValidadeAtencao)
                        alertas.Add(CriarAlerta(TipoAlerta.ATENCAO, insumo, lote, dias,
                            lote.QuantidadeAtual, "O lote está próximo do vencimento."));
                }

                var saldoUtilizavel = lotesDoInsumo
                    .Where(l => l.QuantidadeAtual > 0 && l.DataValidade >= hoje)
                    .Sum(l => l.QuantidadeAtual);

                if (saldoUtilizavel <= 0)
                    alertas.Add(CriarAlerta(TipoAlerta.ESTOQUE_VAZIO, insumo, null, null,
                        saldoUtilizavel, "Não há estoque utilizável para este insumo."));
                else if (saldoUtilizavel < insumo.EstoqueMinimo)
                    alertas.Add(CriarAlerta(TipoAlerta.ESTOQUE_BAIXO, insumo, null, null,
                        saldoUtilizavel, "O estoque utilizável está abaixo do mínimo definido."));
            }

            // Urgentes (severidade maior) antes; em cada grupo, menos dias para vencer primeiro
            var resultado = alertas
                .Where(a => !tipo.HasValue || a.Tipo == tipo.Value)
                .Where(a => !severidade.HasValue || a.Severidade == severidade.Value)
                .OrderByDescending(a => a.Severidade)
                .ThenBy(a => a.DiasParaVencer ?? int.MaxValue)
                .ThenBy(a => a.InsumoNome)
                .ThenBy(a => a.LoteId);

            return (limite is { } max ? resultado.Take(max) : resultado).ToList();
        }

        private static AlertaResponse CriarAlerta(
            TipoAlerta tipo, Insumo insumo, LoteInsumo? lote,
            int? diasParaVencer, decimal saldo, string mensagem)
        {
            var (severidade, acao) = Regra(tipo);
            return new(
                tipo, severidade, acao, insumo.Id, insumo.Nome, insumo.Categoria, insumo.UnidadeMedida,
                lote?.Id, lote?.CodigoLote, lote?.DataValidade, diasParaVencer,
                saldo, insumo.EstoqueMinimo, mensagem);
        }
    }
}
