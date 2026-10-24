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

            // Fila FEFO: só entra quem ainda pode ser consumido, na ordem de uso (regra única em CalculoProducao)
            var fila = CalculoProducao.FilaFefo(lotes, hoje);
            var fora = CalculoProducao.OrdemDeUso(lotes.Where(l => !CalculoProducao.PodeSerConsumido(l, hoje)));

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

        private static ArmarioItemResponse Resumir(Insumo insumo, IEnumerable<LoteInsumo> lotes, DateOnly hoje)
        {
            var validos = CalculoProducao.FilaFefo(lotes, hoje);

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

        // ---------- API-19: resumo por insumo e lotes do insumo ----------

        public async Task<List<InsumoResumoResponse>> ResumirInsumosAsync(
            string? categoria, string? busca, bool incluirInativos, CancellationToken ct = default)
        {
            var filtro = FiltroCategoria.Interpretar(categoria);
            var insumos = await _insumos.ListarAsync(filtro, busca, incluirInativos, ct);
            if (insumos.Count == 0) return new List<InsumoResumoResponse>();

            // Uma única consulta traz os lotes com saldo de todos os insumos; insumo sem lote fica com a lista vazia
            var lotes = await _lotes.ListarPorInsumosAsync(insumos.Select(i => i.Id).ToArray(), false, ct);
            var porInsumo = lotes.ToLookup(l => l.InsumoId);
            var hoje = _relogio.Hoje;

            return insumos.Select(i => InsumoResumoResponse.De(i, porInsumo[i.Id], hoje)).ToList();
        }

        public async Task<InsumoLotesResponse> ObterLotesDoInsumoAsync(long insumoId, CancellationToken ct = default)
        {
            var insumo = await _insumos.ObterPorIdAsync(insumoId, ct)
                ?? throw new RecursoNaoEncontradoException("Insumo", insumoId);

            var lotes = await _lotes.ListarPorInsumosAsync(new[] { insumoId }, true, ct);
            var hoje = _relogio.Hoje;

            var comSaldo = lotes.Where(l => l.QuantidadeAtual > 0).ToList();

            // Primeiro a fila de uso (1, 2, 3...), depois os vencidos que ainda têm saldo (sem posição na fila)
            var respostas = new List<LoteDoInsumoResponse>();
            var posicao = 1;
            foreach (var lote in CalculoProducao.FilaFefo(comSaldo, hoje))
                respostas.Add(LoteDoInsumoResponse.De(lote, insumo, hoje, posicao++));
            foreach (var lote in CalculoProducao.OrdemDeUso(comSaldo.Where(l => !CalculoProducao.PodeSerConsumido(l, hoje))))
                respostas.Add(LoteDoInsumoResponse.De(lote, insumo, hoje, null));

            var esgotados = lotes
                .Where(l => l.QuantidadeAtual <= 0)
                .OrderByDescending(l => l.DataEntrada).ThenByDescending(l => l.Id)
                .Select(l => LoteDoInsumoResponse.De(l, insumo, hoje, null))
                .ToList();

            return new InsumoLotesResponse(
                InsumoResumoResponse.De(insumo, comSaldo, hoje),
                insumo.DiasValidadeCritica, insumo.DiasValidadeAtencao,
                respostas, esgotados);
        }
    }
}
