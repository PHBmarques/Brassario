using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class PerdaService : IPerdaService
    {
        public const int MotivoMinimo = 5;
        public const int MotivoMaximo = 255;
        private const decimal QuantidadeMaxima = 999_999_999m;   // numeric(12,3)

        private readonly IPerdaRepository _repo;
        private readonly IRelogio _relogio;

        public PerdaService(IPerdaRepository repo, IRelogio relogio)
        {
            _repo = repo;
            _relogio = relogio;
        }

        public async Task<PerdaResponse> RegistrarAsync(long loteId, PerdaRequest request, long usuarioId, CancellationToken ct = default)
        {
            var (quantidade, motivo) = Validar(request);

            return await _repo.ExecutarEmTransacaoAsync(async () =>
            {
                var lote = await _repo.BloquearLoteAsync(loteId, ct)
                    ?? throw new RecursoNaoEncontradoException("Lote não encontrado.");

                var saldoAnterior = lote.QuantidadeAtual;
                if (quantidade > saldoAnterior)
                    throw new NegocioException(
                        $"A perda ({quantidade}) é maior que o saldo do lote ({saldoAnterior}).",
                        "PERDA_MAIOR_QUE_SALDO",
                        new Dictionary<string, string[]> { ["quantidade"] = new[] { $"o saldo do lote é {saldoAnterior}" } });

                var insumo = await _repo.ObterInsumoAsync(lote.InsumoId, ct)
                    ?? throw new RecursoNaoEncontradoException("Insumo do lote não encontrado.");

                lote.QuantidadeAtual = saldoAnterior - quantidade;
                lote.Versao++;

                var movimentacao = new MovimentacaoEstoque
                {
                    LoteInsumoId = lote.Id,
                    UsuarioId = usuarioId,
                    Tipo = TipoMovimentacao.PERDA,
                    Quantidade = -quantidade,
                    Motivo = motivo,
                    DataHora = _relogio.AgoraUtc
                };

                await _repo.AdicionarMovimentacaoAsync(movimentacao, ct);
                await _repo.SalvarAsync(ct);

                return new PerdaResponse(
                    movimentacao.Id, lote.Id, lote.CodigoLote, insumo.Id, insumo.Nome, insumo.UnidadeMedida,
                    quantidade, saldoAnterior, lote.QuantidadeAtual, motivo, movimentacao.DataHora);
            }, ct);
        }

        private static (decimal Quantidade, string Motivo) Validar(PerdaRequest request)
        {
            var erros = new Dictionary<string, string[]>();

            var quantidade = 0m;
            if (request.Quantidade is null || request.Quantidade <= 0)
                erros["quantidade"] = new[] { "Informe a quantidade perdida, maior que zero." };
            else if (request.Quantidade > QuantidadeMaxima)
                erros["quantidade"] = new[] { "A quantidade é grande demais." };
            else if (decimal.Round(request.Quantidade.Value, 3) != request.Quantidade.Value)
                erros["quantidade"] = new[] { "A quantidade aceita no máximo 3 casas decimais." };
            else
                quantidade = request.Quantidade.Value;

            var motivo = request.Motivo?.Trim() ?? string.Empty;
            if (motivo.Length < MotivoMinimo)
                erros["motivo"] = new[] { $"Informe o motivo da perda, com pelo menos {MotivoMinimo} caracteres." };
            else if (motivo.Length > MotivoMaximo)
                erros["motivo"] = new[] { $"O motivo tem no máximo {MotivoMaximo} caracteres." };

            if (erros.Count > 0) throw new ValidacaoException(erros);
            return (quantidade, motivo);
        }
    }
}
