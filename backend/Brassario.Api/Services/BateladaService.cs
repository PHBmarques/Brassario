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
            var porInsumo = lotes.ToLookup(l => l.InsumoId);

            // Passo 1: calcula o que sai de cada lote, sem alterar nada ainda
            var plano = new List<(ReceitaItem Item, LoteInsumo Lote, decimal Quantidade)>();
            var faltas = new List<(string Insumo, decimal Necessario, decimal Disponivel)>();
            var soVencidos = new List<(string Insumo, string Lotes)>();

            foreach (var item in itens)
            {
                var necessario = CalculoProducao.Escalar(item.Quantidade, receita.VolumeBaseLitros, litros);
                var fila = CalculoProducao.FilaFefo(porInsumo[item.InsumoId], hoje);
                var disponivel = fila.Sum(l => l.QuantidadeAtual);

                if (disponivel < necessario)
                {
                    // Sem nenhum lote utilizável, mas com saldo em lote vencido: avisa como LOTE_VENCIDO
                    var vencidos = porInsumo[item.InsumoId].Where(l => l.QuantidadeAtual > 0 && !CalculoProducao.PodeSerConsumido(l, hoje)).ToList();
                    if (fila.Count == 0 && vencidos.Count > 0)
                        soVencidos.Add((item.Insumo.Nome, string.Join(", ", CalculoProducao.OrdemDeUso(vencidos).Select(l =>
                            $"{l.CodigoLote} venceu em {l.DataValidade.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}"))));
                    else
                        faltas.Add((item.Insumo.Nome, necessario, disponivel));
                    continue;
                }

                foreach (var (lote, quantidade) in CalculoProducao.Alocar(fila, necessario))
                    plano.Add((item, lote, quantidade));
            }

            // RN-05: tudo ou nada. Se faltar qualquer insumo, nada foi alterado até aqui.
            // Se o único problema é lote vencido: LOTE_VENCIDO. Se há também falta de saldo, ESTOQUE_INSUFICIENTE lista tudo.
            if (faltas.Count == 0 && soVencidos.Count > 0)
                throw new LoteVencidoException(soVencidos);

            if (faltas.Count > 0)
            {
                foreach (var v in soVencidos)
                    faltas.Add((v.Insumo, itens.Where(i => i.Insumo.Nome == v.Insumo)
                        .Select(i => CalculoProducao.Escalar(i.Quantidade, receita.VolumeBaseLitros, litros)).First(), 0m));
                throw new EstoqueInsuficienteException(faltas);
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

            var consumos = new List<BateladaConsumoResponse>();

            foreach (var (item, lote, quantidade) in plano)
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

                consumos.Add(new BateladaConsumoResponse(
                    lote.Id, lote.CodigoLote, lote.DataValidade,
                    item.InsumoId, item.Insumo.Nome, item.Insumo.UnidadeMedida, quantidade, custo));
            }

            batelada.CustoTotal = CalculoProducao.CustoTotal(consumos.Select(c => c.Custo));

            await _bateladas.AdicionarAsync(batelada, ct);
            await _bateladas.SalvarAsync(ct);

            return new BateladaResponse(
                batelada.Id, batelada.Codigo, receita.Id, receita.Nome, litros,
                batelada.Status, batelada.CustoTotal, batelada.DataProducao, batelada.Observacoes, consumos);
        }

        public async Task<List<BateladaConsumoResponse>> ObterConsumosAsync(long bateladaId, CancellationToken ct = default)
        {
            var batelada = await _bateladas.ObterComConsumosAsync(bateladaId, ct)
                ?? throw new RecursoNaoEncontradoException("Produção não encontrada.");

            return batelada.Consumos
                .OrderBy(c => c.LoteInsumo.InsumoId).ThenBy(c => c.Id)
                .Select(c => new BateladaConsumoResponse(
                    c.LoteInsumoId, c.LoteInsumo.CodigoLote, c.LoteInsumo.DataValidade,
                    c.LoteInsumo.InsumoId, c.LoteInsumo.Insumo.Nome, c.LoteInsumo.Insumo.UnidadeMedida,
                    c.Quantidade, c.Custo))
                .ToList();
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
