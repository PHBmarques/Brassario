using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class EntradaService : IEntradaService
    {
        private readonly ILoteService _lotes;
        private readonly IInsumoService _insumos;
        private readonly IFornecedorService _fornecedores;
        private readonly ILoteRepository _loteRepo;
        private readonly IRelogio _relogio;

        public EntradaService(
            ILoteService lotes, IInsumoService insumos, IFornecedorService fornecedores,
            ILoteRepository loteRepo, IRelogio relogio)
        {
            _lotes = lotes;
            _insumos = insumos;
            _fornecedores = fornecedores;
            _loteRepo = loteRepo;
            _relogio = relogio;
        }

        public async Task<EntradaResponse> RegistrarAsync(EntradaRequest request, long usuarioId, CancellationToken ct = default)
        {
            // 1) Todos os erros de campo juntos (400), antes de tocar no banco
            var erros = ValidarCampos(request);
            if (erros.Count > 0) throw new ValidacaoException(erros);

            var loteRequest = new LoteRequest
            {
                InsumoId = request.InsumoId ?? 0,
                FornecedorId = request.FornecedorId,
                CodigoLote = request.Lote!.CodigoLote,
                NotaFiscal = request.Lote.NotaFiscal,
                DataEntrada = request.Lote.DataEntrada,
                DataValidade = request.Lote.DataValidade,
                Quantidade = request.Lote.Quantidade,
                PrecoUnitario = request.Lote.PrecoUnitario
            };

            // 2) Insumo e fornecedor novos: confere nome único (409), mas ainda não grava nada
            var insumoNovo = request.NovoInsumo is null
                ? null
                : await _insumos.PrepararNovoAsync(request.NovoInsumo, "novoInsumo.", ct);

            var fornecedorNovo = request.NovoFornecedor is null
                ? null
                : await _fornecedores.PrepararNovoAsync(request.NovoFornecedor, "novoFornecedor.", ct);

            // 3) Regras do lote e gravação de tudo de uma vez (uma transação só)
            EntradaGravada gravada;
            try
            {
                gravada = await _lotes.RegistrarEntradaCompostaAsync(loteRequest, insumoNovo, fornecedorNovo, usuarioId, ct);
            }
            catch (ConflitoException e) when (e.Detalhes is not null)
            {
                throw CamposComPrefixo.Aplicar(e, "lote.");
            }

            // 4) Resposta: o que foi criado e o saldo do insumo depois da entrada
            var hoje = _relogio.Hoje;
            var lotesDoInsumo = await _loteRepo.ListarPorInsumosAsync(new[] { gravada.Insumo.Id }, false, ct);
            var saldo = lotesDoInsumo
                .Where(l => l.DataValidade >= hoje && l.QuantidadeAtual > 0)
                .Sum(l => l.QuantidadeAtual);

            return new EntradaResponse(
                LoteResponse.De(gravada.Lote, gravada.Insumo, gravada.Fornecedor, hoje),
                InsumoResponse.De(gravada.Insumo),
                gravada.Fornecedor is null ? null : FornecedorResponse.De(gravada.Fornecedor),
                saldo);
        }

        // Regra do card: id OU dados novos, nunca os dois; para o insumo, nunca nenhum dos dois
        private Dictionary<string, string[]> ValidarCampos(EntradaRequest r)
        {
            var erros = new Dictionary<string, string[]>();

            if (r.Lote is null)
                erros["lote"] = new[] { "Informe os dados do lote." };

            if (r.InsumoId.HasValue && r.NovoInsumo is not null)
                erros["insumoId"] = new[] { "Informe o insumo que já existe ou os dados de um novo insumo, não os dois." };
            else if (!r.InsumoId.HasValue && r.NovoInsumo is null)
                erros["insumoId"] = new[] { "Informe o insumo ou cadastre um novo." };
            else if (r.InsumoId is <= 0)
                erros["insumoId"] = new[] { "Informe o insumo." };

            if (r.FornecedorId.HasValue && r.NovoFornecedor is not null)
                erros["fornecedorId"] = new[] { "Informe o fornecedor que já existe ou os dados de um novo fornecedor, não os dois." };

            if (r.Lote is not null)
                Juntar(erros, () => _lotes.ValidarEntrada(
                    new LoteRequest
                    {
                        CodigoLote = r.Lote.CodigoLote,
                        NotaFiscal = r.Lote.NotaFiscal,
                        DataEntrada = r.Lote.DataEntrada,
                        DataValidade = r.Lote.DataValidade,
                        Quantidade = r.Lote.Quantidade,
                        PrecoUnitario = r.Lote.PrecoUnitario
                    }, exigirInsumo: false), "lote.");

            if (r.NovoInsumo is not null)
                Juntar(erros, () => _insumos.ValidarNovo(r.NovoInsumo, "novoInsumo."), null);

            if (r.NovoFornecedor is not null)
                Juntar(erros, () => _fornecedores.ValidarNovo(r.NovoFornecedor, "novoFornecedor."), null);

            return erros;
        }

        // prefixo = null quando o próprio serviço já devolve os campos com prefixo
        private static void Juntar(Dictionary<string, string[]> erros, Action validar, string? prefixo)
        {
            try { validar(); }
            catch (ValidacaoException e)
            {
                var detalhes = prefixo is null ? e.Detalhes : CamposComPrefixo.Mapear(e.Detalhes, prefixo);
                foreach (var (campo, mensagens) in detalhes) erros[campo] = mensagens;
            }
        }
    }
}
