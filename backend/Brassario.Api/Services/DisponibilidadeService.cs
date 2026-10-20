using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class DisponibilidadeService : IDisponibilidadeService
    {
        private readonly IReceitaService _receitas;
        private readonly ILoteRepository _lotes;
        private readonly IRelogio _relogio;

        public DisponibilidadeService(IReceitaService receitas, ILoteRepository lotes, IRelogio relogio)
        {
            _receitas = receitas;
            _lotes = lotes;
            _relogio = relogio;
        }

        public async Task<DisponibilidadeResponse> SimularAsync(long receitaId, decimal? volume, CancellationToken ct = default)
        {
            // 1) Volume (400)
            var litros = ValidarVolume(volume);

            // 2) Receita existente (404) e ativa (422 STATUS_INVALIDO)
            var receita = await _receitas.ObterParaProducaoAsync(receitaId, ct);

            // 3) Uma única consulta de leitura traz os lotes com saldo de todos os insumos da receita.
            //    Nada é gravado e nenhum lote é bloqueado: é só uma simulação (o valor real é conferido ao produzir).
            var hoje = _relogio.Hoje;
            var insumoIds = receita.Itens.Select(i => i.InsumoId).Distinct().ToArray();
            var lotes = await _lotes.ListarPorInsumosAsync(insumoIds, false, ct);
            var porInsumo = lotes.ToLookup(l => l.InsumoId);

            var itens = new List<DisponibilidadeItemResponse>();
            foreach (var item in receita.Itens.OrderBy(i => i.Insumo.Nome))
            {
                // RN-04: escala pela proporção entre o volume pedido e o volume base da receita
                var necessario = CalculoProducao.Escalar(item.Quantidade, receita.VolumeBaseLitros, litros);

                // RN-01 e RN-03: o saldo disponível é a soma dos lotes não vencidos
                var fila = CalculoProducao.FilaFefo(porInsumo[item.InsumoId], hoje);
                var disponivel = fila.Sum(l => l.QuantidadeAtual);

                // RN-02: lotes que seriam consumidos, do que vence primeiro para o que vence depois
                var previstos = CalculoProducao.Alocar(fila, necessario)
                    .Select(p => new LotePrevistoResponse(p.Lote.Id, p.Lote.CodigoLote, p.Lote.DataValidade, p.Quantidade))
                    .ToList();

                var suficiente = disponivel >= necessario;
                var falta = suficiente ? 0m : necessario - disponivel;

                itens.Add(new DisponibilidadeItemResponse(
                    item.InsumoId, item.Insumo.Nome, item.Insumo.UnidadeMedida,
                    necessario, disponivel, suficiente, falta, previstos));
            }

            return new DisponibilidadeResponse(
                receita.Id, receita.Nome, litros, itens.All(i => i.Suficiente), itens);
        }

        // O banco guarda o volume com 2 casas decimais
        private static decimal ValidarVolume(decimal? volume)
        {
            if (volume is null || volume <= 0)
                throw new ValidacaoException("volume", "Informe o volume a produzir, em litros, maior que zero.");

            var arredondado = Math.Round(volume.Value, 2, MidpointRounding.AwayFromZero);
            if (arredondado < 0.01m)
                throw new ValidacaoException("volume", "O volume mínimo é 0,01 litro.");
            if (arredondado > 99_999_999m)
                throw new ValidacaoException("volume", "O volume é grande demais.");

            return arredondado;
        }
    }
}
