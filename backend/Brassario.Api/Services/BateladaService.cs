using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using System.Globalization;

namespace Brassario.Api.Services
{
    public class BateladaService : IBateladaService
    {
        private readonly IReceitaService _receitas;
        private readonly IBateladaRepository _bateladas;
        private readonly IRelogio _relogio;

        public BateladaService(IReceitaService receitas, IBateladaRepository bateladas, IRelogio relogio)
        {
            _receitas = receitas;
            _bateladas = bateladas;
            _relogio = relogio;
        }

        public async Task<BateladaResponse> ProduzirAsync(BateladaRequest request, long usuarioId, CancellationToken ct = default)
        {
            var (litros, observacoes) = Validar(request);

            // 404 se não existe e 422 STATUS_INVALIDO se estiver inativa
            var receita = await _receitas.ObterParaProducaoAsync(request.ReceitaId, ct);

            // RN-06: tudo em uma transação. Qualquer exceção aqui dentro desfaz tudo.
            return await _bateladas.ExecutarEmTransacaoAsync(
                () => ProduzirNaTransacaoAsync(receita, litros, observacoes, usuarioId, ct), ct);
        }

        private async Task<BateladaResponse> ProduzirNaTransacaoAsync(
            Receita receita, decimal litros, string? observacoes, long usuarioId, CancellationToken ct)
        {
            var hoje = _relogio.Hoje;
            var agora = _relogio.AgoraUtc;

            // RN-07: trava os lotes (ordem de insumo e FEFO) antes de conferir o saldo
            var itens = receita.Itens.OrderBy(i => i.InsumoId).ToList();
            var lotes = await _bateladas.BloquearLotesAsync(itens.Select(i => i.InsumoId).ToArray(), ct);

            // Passo 1: calcula o que sai de cada lote, sem alterar nada ainda (mesmo cálculo da prévia)
            var plano = PlanoDeBaixa.Planejar(itens, receita.VolumeBaseLitros, litros, lotes, hoje);

            // RN-05: tudo ou nada. Se faltar qualquer insumo, nada foi alterado até aqui.
            // Se o único problema é lote vencido: LOTE_VENCIDO. Se há também falta de saldo, ESTOQUE_INSUFICIENTE lista tudo.
            if (plano.TemProblema)
            {
                if (!plano.ComFalta.Any())
                    throw new LoteVencidoException(plano.SoComVencido.Select(i => (i.Item.Insumo.Nome, string.Join(", ",
                        i.LotesVencidos.Select(l => $"{l.CodigoLote} venceu em {l.DataValidade.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}")))).ToList());

                throw new EstoqueInsuficienteException(plano.Ingredientes
                    .Where(i => i.Situacao != SituacaoNoPlano.Ok)
                    .Select(i => (i.Item.Insumo.Nome, i.Necessario, i.Disponivel)).ToList());
            }

            // Passo 2: aplica a baixa, o consumo e as movimentações
            var batelada = new Batelada
            {
                Codigo = await _bateladas.ProximoCodigoAsync(hoje.Year, ct),
                ReceitaId = receita.Id,
                UsuarioId = usuarioId,
                VolumePlanejado = litros,
                Status = StatusBatelada.EM_ANDAMENTO,
                DataProducao = agora,
                Observacoes = observacoes
            };

            // Estoque utilizável de cada insumo ANTES da baixa (mesmo conceito do Armário: lote vencido não conta)
            var saldoAntes = SaldoUtilizavelPorInsumo(lotes, hoje);

            var retirados = new List<(long InsumoId, LoteInsumo Lote, decimal Quantidade, decimal Custo)>();

            foreach (var (ingrediente, lote, quantidade) in plano.Retiradas)
            {
                var custo = CalculoProducao.Custo(quantidade, lote.PrecoUnitario);

                lote.QuantidadeAtual -= quantidade;
                lote.Versao++;

                batelada.Consumos.Add(new BateladaConsumo
                {
                    LoteInsumo = lote,
                    Quantidade = quantidade,
                    Custo = custo
                });

                // Saída é negativa no histórico
                batelada.Movimentacoes.Add(new MovimentacaoEstoque
                {
                    LoteInsumo = lote,
                    UsuarioId = usuarioId,
                    Tipo = TipoMovimentacao.SAIDA_PRODUCAO,
                    Quantidade = -quantidade,
                    DataHora = agora
                });

                retirados.Add((ingrediente.Item.InsumoId, lote, quantidade, custo));
            }

            var nomes = itens.GroupBy(i => i.InsumoId).ToDictionary(g => g.Key, g => g.First().Insumo);
            var consumos = retirados.Select(r => new BateladaConsumoResponse(
                r.Lote.Id, r.Lote.CodigoLote, r.Lote.DataValidade,
                r.InsumoId, nomes[r.InsumoId].Nome, nomes[r.InsumoId].UnidadeMedida,
                r.Quantidade, r.Custo, r.Lote.QuantidadeAtual)).ToList();

            batelada.CustoTotal = CalculoProducao.CustoTotal(consumos.Select(c => c.Custo));

            // Estoque utilizável DEPOIS da baixa: os lotes já estão com o saldo novo
            var saldoDepois = SaldoUtilizavelPorInsumo(lotes, hoje);
            var insumos = ResumirPorInsumo(consumos)
                .Select(i => i with
                {
                    SaldoAntes = saldoAntes.GetValueOrDefault(i.InsumoId),
                    SaldoDepois = saldoDepois.GetValueOrDefault(i.InsumoId)
                }).ToList();

            await _bateladas.AdicionarAsync(batelada, ct);
            await _bateladas.SalvarAsync(ct);

            return new BateladaResponse(
                batelada.Id, batelada.Codigo, receita.Id, receita.Nome, litros,
                batelada.Status, batelada.CustoTotal, batelada.DataProducao, batelada.Observacoes, consumos,
                Insumos: insumos);
        }

        private static Dictionary<long, decimal> SaldoUtilizavelPorInsumo(IEnumerable<LoteInsumo> lotes, DateOnly hoje) =>
            lotes.Where(l => CalculoProducao.PodeSerConsumido(l, hoje))
                .GroupBy(l => l.InsumoId)
                .ToDictionary(g => g.Key, g => g.Sum(l => l.QuantidadeAtual));

        private static List<BateladaInsumoResponse> ResumirPorInsumo(IEnumerable<BateladaConsumoResponse> consumos) =>
            consumos.GroupBy(c => c.InsumoId).OrderBy(g => g.Key)
                .Select(g => new BateladaInsumoResponse(
                    g.Key, g.First().Insumo, g.First().UnidadeMedida,
                    g.Sum(c => c.Quantidade), CalculoProducao.CustoTotal(g.Select(c => c.Custo))))
                .ToList();

        /// <summary>Detalhe de uma produção, com a mesma estrutura de consumos da resposta do POST (sem o saldo antes e depois).</summary>
        public async Task<BateladaResponse> ObterAsync(long id, CancellationToken ct = default)
        {
            var batelada = await _bateladas.ObterComConsumosAsync(id, ct)
                ?? throw new RecursoNaoEncontradoException($"Produção não encontrada (id {id}).");

            decimal? rendimento = null, custoPorLitro = null;
            if (batelada.Status == StatusBatelada.FINALIZADA && batelada.VolumeFinal is > 0)
            {
                var volumeFinal = batelada.VolumeFinal.Value;
                rendimento = Math.Round(volumeFinal / batelada.VolumePlanejado * 100m, 2, MidpointRounding.AwayFromZero);
                custoPorLitro = Math.Round(batelada.CustoTotal / volumeFinal, 2, MidpointRounding.AwayFromZero);
            }

            return CriarResposta(batelada, rendimentoPercentual: rendimento, custoPorLitro: custoPorLitro);
        }

        public async Task<BateladaResponse> FinalizarAsync(long id, FinalizarBateladaRequest request, CancellationToken ct = default)
        {
            var volumeFinal = ValidarVolumeFinal(request);
            return await _bateladas.ExecutarEmTransacaoAsync(async () =>
            {
                var batelada = await _bateladas.BloquearBateladaAsync(id, ct)
                    ?? throw new RecursoNaoEncontradoException($"Produção não encontrada (id {id}).");
                ExigirEmAndamento(batelada);

                if (request.VolumeFinal!.Value > batelada.VolumePlanejado * 2m)
                    throw new ValidacaoException("volumeFinal", "O volume final não pode ultrapassar duas vezes o volume planejado.");

                batelada.VolumeFinal = volumeFinal;
                batelada.Status = StatusBatelada.FINALIZADA;
                await _bateladas.SalvarAsync(ct);

                var rendimento = Math.Round(volumeFinal / batelada.VolumePlanejado * 100m, 2, MidpointRounding.AwayFromZero);
                var custoPorLitro = Math.Round(batelada.CustoTotal / volumeFinal, 2, MidpointRounding.AwayFromZero);
                return CriarResposta(batelada, volumeFinal, rendimento, custoPorLitro);
            }, ct);
        }

        public async Task<BateladaResponse> CancelarAsync(long id, CancelarBateladaRequest request, long usuarioId, CancellationToken ct = default)
        {
            var motivo = ValidarMotivoCancelamento(request);
            return await _bateladas.ExecutarEmTransacaoAsync(async () =>
            {
                var batelada = await _bateladas.BloquearBateladaAsync(id, ct)
                    ?? throw new RecursoNaoEncontradoException($"Produção não encontrada (id {id}).");
                ExigirEmAndamento(batelada);

                var loteIds = batelada.Consumos.Select(c => c.LoteInsumoId).Distinct().ToArray();
                var lotes = await _bateladas.BloquearLotesPorIdAsync(loteIds, ct);
                var lotesPorId = lotes.ToDictionary(l => l.Id);
                if (loteIds.Any(loteId => !lotesPorId.ContainsKey(loteId)))
                    throw new RecursoNaoEncontradoException("Lote de insumo", loteIds.First(loteId => !lotesPorId.ContainsKey(loteId)));

                var agora = _relogio.AgoraUtc;
                foreach (var consumo in batelada.Consumos)
                {
                    var lote = lotesPorId[consumo.LoteInsumoId];
                    lote.QuantidadeAtual += consumo.Quantidade;
                    lote.Versao++;

                    batelada.Movimentacoes.Add(new MovimentacaoEstoque
                    {
                        LoteInsumo = lote,
                        UsuarioId = usuarioId,
                        BateladaId = batelada.Id,
                        Tipo = TipoMovimentacao.ESTORNO,
                        Quantidade = consumo.Quantidade,
                        Motivo = motivo,
                        DataHora = agora
                    });
                }

                batelada.Status = StatusBatelada.CANCELADA;
                batelada.MotivoCancelamento = motivo;
                batelada.CanceladoEm = agora;
                await _bateladas.SalvarAsync(ct);
                return CriarResposta(batelada, motivoCancelamento: motivo, incluirEstornos: true);
            }, ct);
        }

        private static void ExigirEmAndamento(Batelada batelada)
        {
            if (batelada.Status != StatusBatelada.EM_ANDAMENTO)
                throw new NegocioException("A produção só pode ser alterada enquanto estiver em andamento.", "STATUS_INVALIDO");
        }

        private static decimal ValidarVolumeFinal(FinalizarBateladaRequest request)
        {
            var erros = new Dictionary<string, string[]>();
            if (request.VolumeFinal is null || request.VolumeFinal <= 0)
                erros["volumeFinal"] = new[] { "Informe o volume final obtido, maior que zero." };
            else if (request.VolumeFinal > 99_999_999m)
                erros["volumeFinal"] = new[] { "O volume final é grande demais." };

            if (erros.Count > 0)
                throw new ValidacaoException(erros);
            var volumeFinal = Math.Round(request.VolumeFinal!.Value, 2, MidpointRounding.AwayFromZero);
            if (volumeFinal <= 0)
                throw new ValidacaoException("volumeFinal", "O volume final deve ser de pelo menos 0,01 litro.");
            return volumeFinal;
        }

        private static string ValidarMotivoCancelamento(CancelarBateladaRequest request)
        {
            var motivo = request.Motivo?.Trim();
            var erros = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(motivo) || motivo.Length < 5)
                erros["motivo"] = new[] { "Informe um motivo com pelo menos 5 caracteres." };
            else if (motivo.Length > 255)
                erros["motivo"] = new[] { "O motivo deve ter no máximo 255 caracteres." };

            if (erros.Count > 0)
                throw new ValidacaoException(erros);
            return motivo!;
        }

        private static BateladaResponse CriarResposta(
            Batelada batelada, decimal? volumeFinal = null, decimal? rendimentoPercentual = null,
            decimal? custoPorLitro = null, string? motivoCancelamento = null, bool incluirEstornos = false)
        {
            var consumos = ConsumosDe(batelada);

            // Produção cancelada: o estoque voltou, então o custo é zero e a resposta traz motivo, data e os estornos
            var cancelada = batelada.Status == StatusBatelada.CANCELADA;

            return new BateladaResponse(
                batelada.Id, batelada.Codigo, batelada.ReceitaId, batelada.Receita.Nome,
                batelada.VolumePlanejado, batelada.Status, cancelada ? 0m : batelada.CustoTotal,
                batelada.DataProducao, batelada.Observacoes, consumos,
                volumeFinal ?? batelada.VolumeFinal, rendimentoPercentual, custoPorLitro,
                motivoCancelamento ?? batelada.MotivoCancelamento,
                incluirEstornos || cancelada ? consumos : null,
                ResumirPorInsumo(consumos),
                batelada.CanceladoEm);
        }

        private static List<BateladaConsumoResponse> ConsumosDe(Batelada batelada) =>
            batelada.Consumos
                .OrderBy(c => c.LoteInsumo.InsumoId).ThenBy(c => c.Id)
                .Select(c => new BateladaConsumoResponse(
                    c.LoteInsumo.Id, c.LoteInsumo.CodigoLote, c.LoteInsumo.DataValidade,
                    c.LoteInsumo.InsumoId, c.LoteInsumo.Insumo.Nome, c.LoteInsumo.Insumo.UnidadeMedida,
                    c.Quantidade, c.Custo, c.LoteInsumo.QuantidadeAtual))
                .ToList();

        public async Task<List<BateladaConsumoResponse>> ObterConsumosAsync(long bateladaId, CancellationToken ct = default)
        {
            var batelada = await _bateladas.ObterComConsumosAsync(bateladaId, ct)
                ?? throw new RecursoNaoEncontradoException("Produção não encontrada.");

            return ConsumosDe(batelada);
        }

        private static (decimal Litros, string? Observacoes) Validar(BateladaRequest request)
        {
            var erros = new Dictionary<string, string[]>();

            if (request.ReceitaId <= 0)
                erros["receitaId"] = new[] { "Informe a receita." };

            decimal litros = 0m;
            if (request.VolumePlanejado is null || request.VolumePlanejado <= 0)
                erros["volumePlanejado"] = new[] { "Informe o volume a produzir, em litros, maior que zero." };
            else
            {
                litros = Math.Round(request.VolumePlanejado.Value, 2, MidpointRounding.AwayFromZero);
                if (litros < 0.01m)
                    erros["volumePlanejado"] = new[] { "O volume mínimo é 0,01 litro." };
                else if (litros > 99_999_999m)
                    erros["volumePlanejado"] = new[] { "O volume é grande demais." };
            }

            var observacoes = string.IsNullOrWhiteSpace(request.Observacoes) ? null : request.Observacoes.Trim();
            if (observacoes is { Length: > 255 })
                erros["observacoes"] = new[] { "As observações têm no máximo 255 caracteres." };

            if (erros.Count > 0)
                throw new ValidacaoException(erros);

            return (litros, observacoes);
        }
    }
}
