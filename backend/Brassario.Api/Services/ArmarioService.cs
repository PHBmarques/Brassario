using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class ArmarioService : IArmarioService
    {
        private readonly IInsumoRepository _insumos;
        private readonly ILoteRepository _lotes;
        private readonly IRelogio _relogio;

        public ArmarioService(IInsumoRepository insumos, ILoteRepository lotes, IRelogio relogio)
        {
            _insumos = insumos;
            _lotes = lotes;
            _relogio = relogio;
        }

        public async Task<List<ArmarioItemResponse>> ListarAsync(
            string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default)
        {
            var filtro = FiltroCategoria.Interpretar(categoria);
            var insumos = await _insumos.ListarAsync(filtro, busca, incluirInativos, ct);
            if (insumos.Count == 0) return new List<ArmarioItemResponse>();

            // Uma única consulta traz os lotes com saldo de todos os insumos da lista
            var lotes = await _lotes.ListarPorInsumosAsync(insumos.Select(i => i.Id).ToArray(), false, ct);
            var porInsumo = lotes.ToLookup(l => l.InsumoId);
            var hoje = _relogio.Hoje;

            return insumos.Select(i => Resumir(i, porInsumo[i.Id], hoje)).ToList();
        }

        public async Task<ArmarioDetalheResponse> ObterDetalheAsync(long insumoId, bool incluirEsgotados, CancellationToken ct = default)
        {
            var insumo = await _insumos.ObterPorIdAsync(insumoId, ct)
                ?? throw new RecursoNaoEncontradoException("Insumo", insumoId);

            var lotes = await _lotes.ListarPorInsumosAsync(new[] { insumoId }, incluirEsgotados, ct);
            var hoje = _relogio.Hoje;

            // Fila FEFO: só entra quem ainda pode ser consumido (não vencido e com saldo), pela validade mais próxima.
            // Empate de validade: quem entrou primeiro sai primeiro.
            var fila = lotes
                .Where(l => PodeSerConsumido(l, hoje))
                .OrderBy(l => l.DataValidade).ThenBy(l => l.DataEntrada).ThenBy(l => l.Id)
                .ToList();

            var fora = lotes
                .Where(l => !PodeSerConsumido(l, hoje))
                .OrderBy(l => l.DataValidade).ThenBy(l => l.DataEntrada).ThenBy(l => l.Id);

            var respostas = new List<ArmarioLoteResponse>();
            var posicao = 1;
            foreach (var lote in fila) respostas.Add(ParaResposta(lote, insumo, hoje, posicao++));
            foreach (var lote in fora) respostas.Add(ParaResposta(lote, insumo, hoje, null));

            return new ArmarioDetalheResponse(
                Resumir(insumo, lotes, hoje),
                insumo.DiasValidadeCritica,
                insumo.DiasValidadeAtencao,
                respostas);
        }

        // ---------- Apoio ----------

        // Lote utilizável: validade de hoje em diante e ainda com saldo (a mesma regra da entrada de estoque)
        private static bool PodeSerConsumido(LoteInsumo l, DateOnly hoje) => l.DataValidade >= hoje && l.QuantidadeAtual > 0;

        private static ArmarioItemResponse Resumir(Insumo insumo, IEnumerable<LoteInsumo> lotes, DateOnly hoje)
        {
            var validos = lotes
                .Where(l => PodeSerConsumido(l, hoje))
                .OrderBy(l => l.DataValidade).ThenBy(l => l.DataEntrada).ThenBy(l => l.Id)
                .ToList();

            var saldo = validos.Sum(l => l.QuantidadeAtual);
            var proximo = validos.FirstOrDefault();

            DateOnly? proximoVencimento = proximo?.DataValidade;
            int? dias = proximo is null ? null : SituacaoValidadeCalculador.DiasParaVencer(proximo.DataValidade, hoje);
            SituacaoValidade? cor = proximo is null
                ? null
                : SituacaoValidadeCalculador.Calcular(proximo.DataValidade, hoje, insumo.DiasValidadeCritica, insumo.DiasValidadeAtencao);

            return new ArmarioItemResponse(
                insumo.Id, insumo.Nome, insumo.Categoria, insumo.UnidadeMedida, insumo.Ativo,
                insumo.EstoqueMinimo, saldo, CalcularStatus(saldo, insumo.EstoqueMinimo),
                validos.Count, proximoVencimento, dias, cor);
        }

        private static StatusEscassez CalcularStatus(decimal saldo, decimal estoqueMinimo)
        {
            if (saldo <= 0) return StatusEscassez.VAZIO;
            return saldo < estoqueMinimo ? StatusEscassez.BAIXO : StatusEscassez.OK;
        }

        private static ArmarioLoteResponse ParaResposta(LoteInsumo l, Insumo insumo, DateOnly hoje, int? ordemFefo) => new(
            l.Id, l.CodigoLote, l.FornecedorId, l.Fornecedor?.Nome,
            l.DataEntrada, l.DataValidade, l.QuantidadeInicial, l.QuantidadeAtual, l.PrecoUnitario,
            SituacaoValidadeCalculador.DiasParaVencer(l.DataValidade, hoje),
            SituacaoValidadeCalculador.Calcular(l.DataValidade, hoje, insumo.DiasValidadeCritica, insumo.DiasValidadeAtencao),
            l.DataValidade < hoje,
            ordemFefo);
    }
}
