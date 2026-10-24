using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class MovimentacaoService : IMovimentacaoService
    {
        private readonly IMovimentacaoRepository _movimentacoes;
        private readonly IRelogio _relogio;

        public MovimentacaoService(IMovimentacaoRepository movimentacoes, IRelogio relogio)
        {
            _movimentacoes = movimentacoes;
            _relogio = relogio;
        }

        public async Task<List<MovimentacaoResponse>> ListarAsync(MovimentacaoFiltro filtro, CancellationToken ct = default)
        {
            if (filtro.DataInicio.HasValue && filtro.DataFim.HasValue && filtro.DataInicio > filtro.DataFim)
                throw new ValidacaoException("dataInicio", "A data inicial não pode ser posterior à data final.");

            return (await _movimentacoes.ListarAsync(filtro, ct)).Select(ParaResposta).ToList();
        }

        public Task<MovimentacaoResponse> RegistrarPerdaAsync(
            long loteId, PerdaRequest request, long usuarioId, CancellationToken ct = default)
        {
            var motivo = ValidarMotivo(request.Motivo);
            if (request.Quantidade <= 0)
                throw new ValidacaoException("quantidade", "A quantidade da perda deve ser maior que zero.");
            ValidarPrecisaoQuantidade(request.Quantidade);

            return RegistrarAsync(loteId, usuarioId, TipoMovimentacao.PERDA, request.Quantidade, motivo, ct);
        }

        public Task<MovimentacaoResponse> RegistrarAjusteAsync(
            long loteId, AjusteRequest request, long usuarioId, CancellationToken ct = default)
        {
            var motivo = ValidarMotivo(request.Motivo);
            if (request.Quantidade == 0)
                throw new ValidacaoException("quantidade", "O ajuste deve ser diferente de zero.");
            ValidarPrecisaoQuantidade(request.Quantidade);

            return RegistrarAsync(loteId, usuarioId, TipoMovimentacao.AJUSTE, request.Quantidade, motivo, ct);
        }

        private Task<MovimentacaoResponse> RegistrarAsync(
            long loteId, long usuarioId, TipoMovimentacao tipo, decimal quantidade, string motivo, CancellationToken ct) =>
            _movimentacoes.ExecutarEmTransacaoAsync(async () =>
            {
                var lote = await _movimentacoes.BloquearLoteAsync(loteId, ct)
                    ?? throw new RecursoNaoEncontradoException("Lote", loteId);

                var saldoNovo = tipo == TipoMovimentacao.PERDA
                    ? lote.QuantidadeAtual - quantidade
                    : lote.QuantidadeAtual + quantidade;

                if (saldoNovo < 0)
                    throw new NegocioException("A operação não pode deixar o saldo do lote negativo.", "SALDO_INSUFICIENTE");
                if (saldoNovo > lote.QuantidadeInicial)
                    throw new NegocioException("O ajuste não pode deixar o saldo acima da quantidade recebida.", "AJUSTE_ACIMA_DO_LIMITE");

                lote.QuantidadeAtual = saldoNovo;
                lote.Versao++;
                var movimentacao = new MovimentacaoEstoque
                {
                    LoteInsumoId = lote.Id,
                    LoteInsumo = lote,
                    UsuarioId = usuarioId,
                    Tipo = tipo,
                    Quantidade = quantidade,
                    Motivo = motivo,
                    DataHora = _relogio.AgoraUtc
                };
                await _movimentacoes.AdicionarAsync(movimentacao, ct);
                await _movimentacoes.SalvarAsync(ct);
                return ParaResposta(movimentacao);
            }, ct);

        private static void ValidarPrecisaoQuantidade(decimal quantidade)
        {
            if (Math.Abs(quantidade) > 999999999.999m || decimal.Round(quantidade, 3) != quantidade)
                throw new ValidacaoException("quantidade", "A quantidade deve ter no máximo 3 casas decimais e caber no saldo do lote.");
        }

        private static string ValidarMotivo(string? motivo)
        {
            var normalizado = motivo?.Trim() ?? string.Empty;
            if (normalizado.Length < 5 || normalizado.Length > 255)
                throw new ValidacaoException("motivo", "O motivo deve ter entre 5 e 255 caracteres após remover os espaços das pontas.");
            return normalizado;
        }

        private static MovimentacaoResponse ParaResposta(MovimentacaoEstoque m) => new(
            m.Id,
            m.LoteInsumoId,
            m.LoteInsumo.CodigoLote,
            m.LoteInsumo.InsumoId,
            m.LoteInsumo.Insumo.Nome,
            m.UsuarioId,
            m.Tipo,
            m.Quantidade,
            m.Motivo,
            m.BateladaId,
            m.DataHora);
    }
}
