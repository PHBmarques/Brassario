using Brassario.Api.DTOs.Request;
using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;

namespace Brassario.Api.Services
{
    public class LoteService : ILoteService
    {
        private readonly ILoteRepository _lotes;
        private readonly IInsumoRepository _insumos;
        private readonly IFornecedorRepository _fornecedores;
        private readonly IRelogio _relogio;

        public LoteService(ILoteRepository lotes, IInsumoRepository insumos, IFornecedorRepository fornecedores, IRelogio relogio)
        {
            _lotes = lotes;
            _insumos = insumos;
            _fornecedores = fornecedores;
            _relogio = relogio;
        }

        public async Task<LoteResponse> RegistrarEntradaAsync(LoteRequest request, long usuarioId, CancellationToken ct = default)
        {
            var gravada = await RegistrarEntradaCompostaAsync(request, null, null, usuarioId, ct);
            return LoteResponse.De(gravada.Lote, gravada.Insumo, gravada.Fornecedor, _relogio.Hoje);
        }

        public void ValidarEntrada(LoteRequest request, bool exigirInsumo = true) =>
            Validar(request, _relogio.Hoje, exigirInsumo);

        public async Task<EntradaGravada> RegistrarEntradaCompostaAsync(
            LoteRequest request, Insumo? insumoNovo, Fornecedor? fornecedorNovo, long usuarioId, CancellationToken ct = default)
        {
            var hoje = _relogio.Hoje;

            // 1) Campos (400)
            var (codigo, validade, entrada, notaFiscal) = Validar(request, hoje, exigirInsumo: insumoNovo is null);

            // 2) Insumo e fornecedor precisam existir (404), a não ser que sejam novos
            var insumo = insumoNovo
                ?? await _insumos.ObterPorIdAsync(request.InsumoId, ct)
                ?? throw new RecursoNaoEncontradoException("Insumo", request.InsumoId);

            var fornecedor = fornecedorNovo;
            if (fornecedor is null && request.FornecedorId.HasValue)
                fornecedor = await _fornecedores.ObterPorIdAsync(request.FornecedorId.Value, ct)
                    ?? throw new RecursoNaoEncontradoException("Fornecedor", request.FornecedorId.Value);

            // 3) Regras de negócio (422)
            if (!insumo.Ativo)
                throw new NegocioException($"O insumo \"{insumo.Nome}\" está inativo e não pode receber entradas.", "INSUMO_INATIVO");

            if (fornecedor is { Ativo: false })
                throw new NegocioException($"O fornecedor \"{fornecedor.Nome}\" está inativo.", "FORNECEDOR_INATIVO");

            if (validade < hoje)
                throw new NegocioException("A data de validade já passou. Não é possível receber um lote vencido.", "VALIDADE_VENCIDA");

            // 4) Código do lote: se não veio, o sistema gera L-AAAAMMDD-NN (NN = ordem do dia para este insumo).
            //    Insumo novo (Id ainda 0) não tem lote nenhum: não há o que consultar.
            var insumoJaExiste = insumo.Id != 0;
            if (codigo.Length == 0)
            {
                var prefixo = $"L-{entrada:yyyyMMdd}-";
                var sequencial = insumoJaExiste ? await _lotes.ProximoSequencialAsync(insumo.Id, prefixo, ct) : 1;
                codigo = $"{prefixo}{sequencial:00}";
            }
            // Código repetido para o mesmo insumo (409). O índice único do banco é a barreira final.
            else if (insumoJaExiste && await _lotes.ExisteCodigoAsync(insumo.Id, codigo, ct))
            {
                var mensagem = $"Já existe um lote com o código \"{codigo}\" para o insumo \"{insumo.Nome}\". Informe outro código.";
                throw new ConflitoException(mensagem, "CODIGO_LOTE_DUPLICADO",
                    new Dictionary<string, string[]> { ["codigoLote"] = new[] { mensagem } });
            }

            // 5) Lote com saldo atual igual ao inicial + movimentação ENTRADA do usuário logado.
            //    O insumo e o fornecedor novos entram pelo lote (navegação), então um único SaveChanges
            //    grava tudo junto, numa transação só.
            var lote = new LoteInsumo
            {
                InsumoId = insumo.Id,
                Insumo = insumo,
                FornecedorId = fornecedor is { Id: > 0 } ? fornecedor.Id : null,
                Fornecedor = fornecedor,
                CodigoLote = codigo,
                NotaFiscal = notaFiscal,
                DataEntrada = entrada,
                DataValidade = validade,
                QuantidadeInicial = request.Quantidade,
                QuantidadeAtual = request.Quantidade,
                PrecoUnitario = request.PrecoUnitario,
                Versao = 0
            };

            lote.Movimentacoes.Add(new MovimentacaoEstoque
            {
                LoteInsumo = lote,
                UsuarioId = usuarioId,
                Tipo = TipoMovimentacao.ENTRADA,
                Quantidade = request.Quantidade,
                DataHora = _relogio.AgoraUtc
            });

            await _lotes.AdicionarAsync(lote, ct);
            await _lotes.SalvarAsync(ct);

            return new EntradaGravada(lote, insumo, fornecedor);
        }

        private static (string Codigo, DateOnly Validade, DateOnly Entrada, string? NotaFiscal) Validar(LoteRequest r, DateOnly hoje, bool exigirInsumo = true)
        {
            var erros = new Dictionary<string, string[]>();

            if (exigirInsumo && r.InsumoId <= 0)
                erros["insumoId"] = new[] { "Informe o insumo." };

            // Código: espaços das pontas cortados e tudo em maiúsculas. Vazio é permitido (o sistema gera um código).
            var codigo = (r.CodigoLote ?? string.Empty).Trim().ToUpperInvariant();
            if (codigo.Length > 40)
                erros["codigoLote"] = new[] { "O código do lote deve ter no máximo 40 caracteres." };

            // NF opcional: em branco vira nulo; espaços das pontas são cortados
            var notaFiscal = string.IsNullOrWhiteSpace(r.NotaFiscal) ? null : r.NotaFiscal.Trim();
            if (notaFiscal is { Length: > 30 })
                erros["notaFiscal"] = new[] { "O número da nota fiscal deve ter no máximo 30 caracteres." };

            if (r.Quantidade <= 0)
                erros["quantidade"] = new[] { "A quantidade deve ser maior que zero." };

            if (r.PrecoUnitario < 0)
                erros["precoUnitario"] = new[] { "O preço unitário não pode ser negativo." };

            if (!r.DataValidade.HasValue)
                erros["dataValidade"] = new[] { "A data de validade é obrigatória." };

            var entrada = r.DataEntrada ?? hoje;
            if (entrada > hoje)
                erros["dataEntrada"] = new[] { "A data de entrada não pode ser futura." };

            if (erros.Count > 0) throw new ValidacaoException(erros);

            return (codigo, r.DataValidade!.Value, entrada, notaFiscal);
        }
    }
}