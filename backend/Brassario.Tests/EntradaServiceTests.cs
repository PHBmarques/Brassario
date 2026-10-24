using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-18: entrada composta (lote + insumo novo + fornecedor novo) com um banco falso que imita o SaveChanges.</summary>
    public class EntradaServiceTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 19);

        // ---------- Banco falso: guarda tudo em listas e só "grava" no Salvar, tudo ou nada ----------

        private class BancoFalso
        {
            public List<Insumo> Insumos { get; } = new();
            public List<Fornecedor> Fornecedores { get; } = new();
            public List<LoteInsumo> Lotes { get; } = new();
            public List<object> Pendentes { get; } = new();
            public bool FalharNoSalvar { get; set; }
            public int Salvamentos { get; private set; }
            private long _sequencia = 100;

            public void Confirmar()
            {
                Salvamentos++;
                if (FalharNoSalvar)
                {
                    Pendentes.Clear();   // como a transação: falhou, nada fica
                    throw new InvalidOperationException("Falha simulada ao gravar.");
                }

                foreach (var item in Pendentes.ToList())
                {
                    switch (item)
                    {
                        case Insumo i: Gravar(i); break;
                        case Fornecedor f: Gravar(f); break;
                        case LoteInsumo l:
                            Gravar(l.Insumo);
                            l.InsumoId = l.Insumo.Id;
                            if (l.Fornecedor is not null) { Gravar(l.Fornecedor); l.FornecedorId = l.Fornecedor.Id; }
                            l.Id = ++_sequencia;
                            Lotes.Add(l);
                            break;
                    }
                }
                Pendentes.Clear();
            }

            private void Gravar(Insumo i) { if (!Insumos.Contains(i)) { if (i.Id == 0) i.Id = ++_sequencia; Insumos.Add(i); } }
            private void Gravar(Fornecedor f) { if (!Fornecedores.Contains(f)) { if (f.Id == 0) f.Id = ++_sequencia; Fornecedores.Add(f); } }
        }

        private class RelogioFixo : IRelogio
        {
            public DateOnly Hoje => EntradaServiceTests.Hoje;
            public DateTime AgoraUtc => new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        }

        private class InsumoRepoFalso : IInsumoRepository
        {
            private readonly BancoFalso _b;
            public InsumoRepoFalso(BancoFalso b) => _b = b;

            public Task<List<Insumo>> ListarAsync(CategoriaInsumo? c, string? busca, bool incluirInativos, CancellationToken ct = default) =>
                Task.FromResult(_b.Insumos.Where(i => incluirInativos || i.Ativo).OrderBy(i => i.Nome).ToList());
            public Task<Insumo?> ObterPorIdAsync(long id, CancellationToken ct = default) => Task.FromResult(_b.Insumos.FirstOrDefault(i => i.Id == id));
            public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default) =>
                Task.FromResult(_b.Insumos.Any(i => string.Equals(i.Nome, nome.Trim(), StringComparison.OrdinalIgnoreCase) && i.Id != ignorarId));
            public Task AdicionarAsync(Insumo insumo, CancellationToken ct = default) { _b.Pendentes.Add(insumo); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) { _b.Confirmar(); return Task.CompletedTask; }
        }

        private class FornecedorRepoFalso : IFornecedorCadastroRepository
        {
            private readonly BancoFalso _b;
            public FornecedorRepoFalso(BancoFalso b) => _b = b;

            public Task<Fornecedor?> ObterPorIdAsync(long id, CancellationToken ct = default) => Task.FromResult(_b.Fornecedores.FirstOrDefault(f => f.Id == id));
            public Task<List<Fornecedor>> ListarAsync(string? busca, CancellationToken ct = default) => Task.FromResult(_b.Fornecedores.OrderBy(f => f.Nome).ToList());
            public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default) =>
                Task.FromResult(_b.Fornecedores.Any(f => string.Equals(f.Nome, nome.Trim(), StringComparison.OrdinalIgnoreCase) && f.Id != ignorarId));
            public Task AdicionarAsync(Fornecedor fornecedor, CancellationToken ct = default) { _b.Pendentes.Add(fornecedor); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) { _b.Confirmar(); return Task.CompletedTask; }
        }

        private class LoteRepoFalso : ILoteRepository
        {
            private readonly BancoFalso _b;
            public LoteRepoFalso(BancoFalso b) => _b = b;

            public Task<bool> ExisteCodigoAsync(long insumoId, string codigoLote, CancellationToken ct = default) =>
                Task.FromResult(_b.Lotes.Any(l => l.InsumoId == insumoId && string.Equals(l.CodigoLote, codigoLote.Trim(), StringComparison.OrdinalIgnoreCase)));

            public Task<int> ProximoSequencialAsync(long insumoId, string prefixo, CancellationToken ct = default)
            {
                var maior = _b.Lotes
                    .Where(l => l.InsumoId == insumoId && l.CodigoLote.StartsWith(prefixo, StringComparison.Ordinal))
                    .Select(l => int.TryParse(l.CodigoLote.AsSpan(prefixo.Length), out var n) ? n : 0)
                    .DefaultIfEmpty(0).Max();
                return Task.FromResult(maior + 1);
            }

            public Task AdicionarAsync(LoteInsumo lote, CancellationToken ct = default) { _b.Pendentes.Add(lote); return Task.CompletedTask; }
            public Task SalvarAsync(CancellationToken ct = default) { _b.Confirmar(); return Task.CompletedTask; }

            public Task<List<LoteInsumo>> ListarPorInsumosAsync(IReadOnlyCollection<long> insumoIds, bool incluirEsgotados, CancellationToken ct = default) =>
                Task.FromResult(_b.Lotes.Where(l => insumoIds.Contains(l.InsumoId) && (incluirEsgotados || l.QuantidadeAtual > 0)).ToList());
        }

        private class Cenario
        {
            public BancoFalso Banco { get; } = new();
            public EntradaService Entradas { get; }
            public InsumoService Insumos { get; }
            public FornecedorService Fornecedores { get; }
            public LoteService Lotes { get; }

            public Cenario()
            {
                var relogio = new RelogioFixo();
                var insumoRepo = new InsumoRepoFalso(Banco);
                var fornecedorRepo = new FornecedorRepoFalso(Banco);
                var loteRepo = new LoteRepoFalso(Banco);

                Insumos = new InsumoService(insumoRepo, loteRepo, relogio);
                Fornecedores = new FornecedorService(fornecedorRepo);
                Lotes = new LoteService(loteRepo, insumoRepo, fornecedorRepo, relogio);
                Entradas = new EntradaService(Lotes, Insumos, Fornecedores, loteRepo, relogio);

                // Já existem: Malte Pilsen (id 1) e o fornecedor Maltaria Sul (id 1)
                Banco.Insumos.Add(new Insumo
                {
                    Id = 1, Nome = "Malte Pilsen", Categoria = CategoriaInsumo.MALTE, UnidadeMedida = UnidadeMedida.KG,
                    DiasValidadeCritica = 15, DiasValidadeAtencao = 45, Ativo = true
                });
                Banco.Fornecedores.Add(new Fornecedor { Id = 1, Nome = "Maltaria Sul", Ativo = true });
            }
        }

        private static EntradaLoteRequest Lote(string? codigo = "L-001", decimal quantidade = 25, int diasParaVencer = 120) => new()
        {
            CodigoLote = codigo,
            DataValidade = Hoje.AddDays(diasParaVencer),
            Quantidade = quantidade,
            PrecoUnitario = 8.5m
        };

        private static InsumoRequest InsumoNovo(string nome = "Lúpulo Cascade") => new()
        {
            Nome = nome,
            Categoria = "LUPULO",
            UnidadeMedida = "G",
            EstoqueMinimo = 500,
            DiasValidadeCritica = 30,
            DiasValidadeAtencao = 90
        };

        private static FornecedorRequest FornecedorNovo(string nome = "Hop Brasil") => new() { Nome = nome, Contato = "contato@hopbrasil.example" };

        private static EntradaRequest SoLote(EntradaLoteRequest? lote = null) => new() { Lote = lote ?? Lote(), InsumoId = 1, FornecedorId = 1 };

        private static void NadaGravado(Cenario c)
        {
            Assert.Empty(c.Banco.Lotes);
            Assert.Single(c.Banco.Insumos);        // só o que já existia
            Assert.Single(c.Banco.Fornecedores);   // só o que já existia
        }

        // ---------- Os quatro caminhos felizes ----------

        [Fact]
        public async Task Entrada_so_com_lote_usa_insumo_e_fornecedor_que_ja_existem()
        {
            var c = new Cenario();

            var r = await c.Entradas.RegistrarAsync(SoLote(), usuarioId: 7);

            Assert.Equal("L-001", r.Lote.CodigoLote);
            Assert.Equal(1, r.Insumo.Id);
            Assert.Equal("Malte Pilsen", r.Insumo.Nome);
            Assert.Equal(1, r.Fornecedor!.Id);
            Assert.Equal(25, r.SaldoAtual);
            Assert.Single(c.Banco.Lotes);
            Assert.Single(c.Banco.Insumos);        // nenhum insumo novo
            Assert.Single(c.Banco.Fornecedores);   // nenhum fornecedor novo
            Assert.Equal(1, c.Banco.Salvamentos);  // uma gravação só
        }

        [Fact]
        public async Task Entrada_com_insumo_novo_cadastra_o_insumo_e_o_lote_juntos()
        {
            var c = new Cenario();

            var r = await c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(quantidade: 800), NovoInsumo = InsumoNovo(), FornecedorId = 1 }, 7);

            var insumo = c.Banco.Insumos.Single(i => i.Nome == "Lúpulo Cascade");
            Assert.True(insumo.Id > 100);
            Assert.True(insumo.Ativo);
            Assert.Equal(CategoriaInsumo.LUPULO, insumo.Categoria);
            Assert.Equal(UnidadeMedida.G, insumo.UnidadeMedida);
            Assert.Equal(500, insumo.EstoqueMinimo);
            Assert.Equal(30, insumo.DiasValidadeCritica);
            Assert.Equal(90, insumo.DiasValidadeAtencao);

            Assert.Equal(insumo.Id, r.Insumo.Id);
            Assert.Equal(insumo.Id, r.Lote.InsumoId);
            Assert.Equal(insumo.Id, Assert.Single(c.Banco.Lotes).InsumoId);
            Assert.Equal(800, r.SaldoAtual);   // insumo novo: o saldo é só este lote
            Assert.Equal(1, c.Banco.Salvamentos);
        }

        [Fact]
        public async Task Entrada_com_fornecedor_novo_cadastra_o_fornecedor_e_o_lote_juntos()
        {
            var c = new Cenario();

            var r = await c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), InsumoId = 1, NovoFornecedor = FornecedorNovo() }, 7);

            var fornecedor = c.Banco.Fornecedores.Single(f => f.Nome == "Hop Brasil");
            Assert.True(fornecedor.Id > 100);
            Assert.True(fornecedor.Ativo);
            Assert.Equal("contato@hopbrasil.example", fornecedor.Contato);
            Assert.Equal(fornecedor.Id, r.Fornecedor!.Id);
            Assert.Equal(fornecedor.Id, r.Lote.FornecedorId);
            Assert.Equal("Hop Brasil", r.Lote.FornecedorNome);
            Assert.Equal(fornecedor.Id, Assert.Single(c.Banco.Lotes).FornecedorId);
            Assert.Equal(1, c.Banco.Salvamentos);
        }

        [Fact]
        public async Task Entrada_com_insumo_novo_e_fornecedor_novo_grava_os_dois_e_o_lote()
        {
            var c = new Cenario();

            var r = await c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(quantidade: 300), NovoInsumo = InsumoNovo(), NovoFornecedor = FornecedorNovo() }, 7);

            Assert.Equal(2, c.Banco.Insumos.Count);
            Assert.Equal(2, c.Banco.Fornecedores.Count);
            var lote = Assert.Single(c.Banco.Lotes);
            Assert.Equal(r.Insumo.Id, lote.InsumoId);
            Assert.Equal(r.Fornecedor!.Id, lote.FornecedorId);
            Assert.Equal(300, r.SaldoAtual);
            Assert.Equal(1, c.Banco.Salvamentos);
        }

        [Fact]
        public async Task Fornecedor_e_opcional_insumo_cadastrado_sem_fornecedor()
        {
            var c = new Cenario();

            var r = await c.Entradas.RegistrarAsync(new EntradaRequest { Lote = Lote(), NovoInsumo = InsumoNovo() }, 7);

            Assert.Null(r.Fornecedor);
            Assert.Null(r.Lote.FornecedorId);
            Assert.Null(Assert.Single(c.Banco.Lotes).FornecedorId);
            Assert.Single(c.Banco.Fornecedores);   // nenhum fornecedor criado
        }

        // ---------- Movimentação, resposta e saldo ----------

        [Fact]
        public async Task Registra_a_movimentacao_ENTRADA_do_usuario_junto_com_o_lote()
        {
            var c = new Cenario();

            await c.Entradas.RegistrarAsync(new EntradaRequest { Lote = Lote(quantidade: 40), NovoInsumo = InsumoNovo() }, usuarioId: 7);

            var mov = Assert.Single(Assert.Single(c.Banco.Lotes).Movimentacoes);
            Assert.Equal(TipoMovimentacao.ENTRADA, mov.Tipo);
            Assert.Equal(7, mov.UsuarioId);
            Assert.Equal(40, mov.Quantidade);
        }

        [Fact]
        public async Task Saldo_atual_soma_os_lotes_que_ja_existiam_e_ignora_vencido_e_esgotado()
        {
            var c = new Cenario();
            LoteInsumo Antigo(long id, decimal saldo, int validadeEmDias) => new()
            {
                Id = id, InsumoId = 1, CodigoLote = $"ANT-{id}", DataEntrada = Hoje.AddDays(-30),
                DataValidade = Hoje.AddDays(validadeEmDias), QuantidadeInicial = 100, QuantidadeAtual = saldo
            };
            c.Banco.Lotes.Add(Antigo(1, 100, 60));    // vale
            c.Banco.Lotes.Add(Antigo(2, 50, -1));     // vencido
            c.Banco.Lotes.Add(Antigo(3, 0, 60));      // esgotado

            var r = await c.Entradas.RegistrarAsync(SoLote(Lote(quantidade: 25)), 7);

            Assert.Equal(125, r.SaldoAtual);          // 100 + 25
        }

        [Fact]
        public async Task Resposta_traz_lote_insumo_fornecedor_e_saldo()
        {
            var c = new Cenario();
            var lote = Lote("l-9");
            lote.NotaFiscal = " NF-1 ";

            var r = await c.Entradas.RegistrarAsync(new EntradaRequest { Lote = lote, NovoInsumo = InsumoNovo(), NovoFornecedor = FornecedorNovo() }, 7);

            Assert.Equal("L-9", r.Lote.CodigoLote);
            Assert.Equal("NF-1", r.Lote.NotaFiscal);
            Assert.Equal("Lúpulo Cascade", r.Insumo.Nome);
            Assert.Equal("Hop Brasil", r.Fornecedor!.Nome);
            Assert.Equal(25, r.SaldoAtual);
        }

        [Fact]
        public async Task Codigo_vazio_com_insumo_novo_gera_L_AAAAMMDD_01()
        {
            var c = new Cenario();

            var r = await c.Entradas.RegistrarAsync(new EntradaRequest { Lote = Lote(codigo: ""), NovoInsumo = InsumoNovo() }, 7);

            Assert.Equal("L-20261019-01", r.Lote.CodigoLote);
        }

        // ---------- Regra: id OU dados novos ----------

        [Fact]
        public async Task Insumo_por_id_e_insumo_novo_ao_mesmo_tempo_vira_400_no_campo_insumoId()
        {
            var c = new Cenario();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), InsumoId = 1, NovoInsumo = InsumoNovo() }, 7));

            Assert.Contains("insumoId", ex.Detalhes.Keys);
            NadaGravado(c);
        }

        [Fact]
        public async Task Sem_insumo_nenhum_vira_400_no_campo_insumoId()
        {
            var c = new Cenario();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Entradas.RegistrarAsync(new EntradaRequest { Lote = Lote() }, 7));

            Assert.Contains("insumoId", ex.Detalhes.Keys);
            NadaGravado(c);
        }

        [Fact]
        public async Task Fornecedor_por_id_e_fornecedor_novo_ao_mesmo_tempo_vira_400_no_campo_fornecedorId()
        {
            var c = new Cenario();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), InsumoId = 1, FornecedorId = 1, NovoFornecedor = FornecedorNovo() }, 7));

            Assert.Contains("fornecedorId", ex.Detalhes.Keys);
            NadaGravado(c);
        }

        [Fact]
        public async Task Sem_lote_vira_400_no_campo_lote()
        {
            var c = new Cenario();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Entradas.RegistrarAsync(new EntradaRequest { InsumoId = 1 }, 7));

            Assert.Contains("lote", ex.Detalhes.Keys);
        }

        // ---------- Erros por campo ----------

        [Fact]
        public async Task Erros_do_insumo_novo_e_do_lote_vem_juntos_cada_um_no_seu_campo_e_nada_e_gravado()
        {
            var c = new Cenario();
            var insumo = InsumoNovo();
            insumo.Nome = "   ";
            insumo.Categoria = "BEBIDA";

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(quantidade: 0), NovoInsumo = insumo, NovoFornecedor = new FornecedorRequest { Nome = "" } }, 7));

            Assert.Contains("novoInsumo.nome", ex.Detalhes.Keys);
            Assert.Contains("novoInsumo.categoria", ex.Detalhes.Keys);
            Assert.Contains("novoFornecedor.nome", ex.Detalhes.Keys);
            Assert.Contains("lote.quantidade", ex.Detalhes.Keys);
            NadaGravado(c);
        }

        [Fact]
        public async Task Insumo_novo_respeita_as_regras_dos_dias_de_alerta()
        {
            var c = new Cenario();
            var insumo = InsumoNovo();
            insumo.DiasValidadeCritica = 30;
            insumo.DiasValidadeAtencao = 10;   // atenção menor que crítica: inválido

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), NovoInsumo = insumo }, 7));

            Assert.Contains("novoInsumo.diasValidadeAtencao", ex.Detalhes.Keys);
            NadaGravado(c);
        }

        // ---------- Conflitos ----------

        [Fact]
        public async Task Nome_de_insumo_repetido_vira_409_NOME_DUPLICADO_no_campo_do_insumo_e_nada_e_gravado()
        {
            var c = new Cenario();

            var ex = await Assert.ThrowsAsync<ConflitoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), NovoInsumo = InsumoNovo("  malte PILSEN "), NovoFornecedor = FornecedorNovo() }, 7));

            Assert.Equal("NOME_DUPLICADO", ex.Codigo);
            Assert.Contains("Já existe um insumo com o nome", ex.Message);
            var campo = Assert.Single(ex.Detalhes!);
            Assert.Equal("novoInsumo.nome", campo.Key);
            NadaGravado(c);   // nem o fornecedor novo ficou
        }

        [Fact]
        public async Task Nome_de_fornecedor_repetido_vira_409_NOME_DUPLICADO_no_campo_do_fornecedor_e_nada_e_gravado()
        {
            var c = new Cenario();

            var ex = await Assert.ThrowsAsync<ConflitoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), NovoInsumo = InsumoNovo(), NovoFornecedor = FornecedorNovo("maltaria sul") }, 7));

            Assert.Equal("NOME_DUPLICADO", ex.Codigo);
            Assert.Equal("novoFornecedor.nome", Assert.Single(ex.Detalhes!).Key);
            NadaGravado(c);   // nem o insumo novo ficou
        }

        [Fact]
        public async Task Codigo_de_lote_repetido_para_insumo_que_ja_existe_vira_409_no_campo_lote_codigoLote()
        {
            var c = new Cenario();
            await c.Entradas.RegistrarAsync(SoLote(Lote("ABC-1")), 7);

            var ex = await Assert.ThrowsAsync<ConflitoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote("abc-1"), InsumoId = 1, NovoFornecedor = FornecedorNovo() }, 7));

            Assert.Equal("CODIGO_LOTE_DUPLICADO", ex.Codigo);
            Assert.Equal("lote.codigoLote", Assert.Single(ex.Detalhes!).Key);
            Assert.Single(c.Banco.Lotes);
            Assert.Single(c.Banco.Fornecedores);   // o fornecedor novo não ficou
        }

        // ---------- Outras regras (404 e 422) ----------

        [Fact]
        public async Task Insumo_que_nao_existe_vira_404_e_nao_grava_o_fornecedor_novo()
        {
            var c = new Cenario();

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), InsumoId = 999, NovoFornecedor = FornecedorNovo() }, 7));

            NadaGravado(c);
        }

        [Fact]
        public async Task Fornecedor_que_nao_existe_vira_404_e_nao_grava_o_insumo_novo()
        {
            var c = new Cenario();

            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), NovoInsumo = InsumoNovo(), FornecedorId = 999 }, 7));

            NadaGravado(c);
        }

        [Fact]
        public async Task Insumo_inativo_vira_422_INSUMO_INATIVO()
        {
            var c = new Cenario();
            c.Banco.Insumos[0].Ativo = false;

            var ex = await Assert.ThrowsAsync<NegocioException>(() => c.Entradas.RegistrarAsync(SoLote(), 7));

            Assert.Equal("INSUMO_INATIVO", ex.Codigo);
            NadaGravado(c);
        }

        [Fact]
        public async Task Fornecedor_inativo_vira_422_e_nao_grava_o_insumo_novo()
        {
            var c = new Cenario();
            c.Banco.Fornecedores[0].Ativo = false;

            var ex = await Assert.ThrowsAsync<NegocioException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), NovoInsumo = InsumoNovo(), FornecedorId = 1 }, 7));

            Assert.Equal("FORNECEDOR_INATIVO", ex.Codigo);
            NadaGravado(c);
        }

        [Fact]
        public async Task Validade_vencida_vira_422_e_nao_grava_insumo_nem_fornecedor_novos()
        {
            var c = new Cenario();

            var ex = await Assert.ThrowsAsync<NegocioException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(diasParaVencer: -1), NovoInsumo = InsumoNovo(), NovoFornecedor = FornecedorNovo() }, 7));

            Assert.Equal("VALIDADE_VENCIDA", ex.Codigo);
            NadaGravado(c);
        }

        // ---------- Falha no meio: nada gravado ----------

        [Fact]
        public async Task Falha_ao_gravar_nao_deixa_insumo_nem_fornecedor_sem_lote()
        {
            var c = new Cenario();
            c.Banco.FalharNoSalvar = true;

            await Assert.ThrowsAsync<InvalidOperationException>(() => c.Entradas.RegistrarAsync(
                new EntradaRequest { Lote = Lote(), NovoInsumo = InsumoNovo(), NovoFornecedor = FornecedorNovo() }, 7));

            NadaGravado(c);
            Assert.Equal(1, c.Banco.Salvamentos);   // uma tentativa só: tudo ou nada
        }

        // ---------- Os endpoints atuais continuam funcionando ----------

        [Fact]
        public async Task POST_insumos_continua_criando_e_agora_o_nome_repetido_traz_NOME_DUPLICADO_no_campo_nome()
        {
            var c = new Cenario();

            var criado = await c.Insumos.CriarAsync(InsumoNovo("Cevada"));
            Assert.True(criado.Id > 0);
            Assert.True(criado.Ativo);

            var ex = await Assert.ThrowsAsync<ConflitoException>(() => c.Insumos.CriarAsync(InsumoNovo("cevada")));
            Assert.Equal("NOME_DUPLICADO", ex.Codigo);
            Assert.Equal("nome", Assert.Single(ex.Detalhes!).Key);
        }

        [Fact]
        public async Task POST_fornecedores_continua_criando_e_o_nome_repetido_traz_NOME_DUPLICADO_no_campo_nome()
        {
            var c = new Cenario();

            var criado = await c.Fornecedores.CriarAsync(FornecedorNovo("Cevadas do Sul"));
            Assert.True(criado.Id > 0);

            var ex = await Assert.ThrowsAsync<ConflitoException>(() => c.Fornecedores.CriarAsync(FornecedorNovo("CEVADAS DO SUL")));
            Assert.Equal("NOME_DUPLICADO", ex.Codigo);
            Assert.Equal("nome", Assert.Single(ex.Detalhes!).Key);
        }

        [Fact]
        public async Task POST_lotes_continua_registrando_a_entrada_de_insumo_e_fornecedor_existentes()
        {
            var c = new Cenario();

            var resposta = await c.Lotes.RegistrarEntradaAsync(new LoteRequest
            {
                InsumoId = 1, FornecedorId = 1, CodigoLote = "l-77",
                DataValidade = Hoje.AddDays(90), Quantidade = 10, PrecoUnitario = 5
            }, usuarioId: 7);

            Assert.Equal("L-77", resposta.CodigoLote);
            Assert.Single(c.Banco.Lotes);
        }

        [Fact]
        public async Task POST_lotes_com_codigo_repetido_traz_o_campo_codigoLote()
        {
            var c = new Cenario();
            var pedido = new LoteRequest { InsumoId = 1, CodigoLote = "X-1", DataValidade = Hoje.AddDays(90), Quantidade = 10, PrecoUnitario = 5 };
            await c.Lotes.RegistrarEntradaAsync(pedido, 7);

            var ex = await Assert.ThrowsAsync<ConflitoException>(() => c.Lotes.RegistrarEntradaAsync(pedido, 7));

            Assert.Equal("CODIGO_LOTE_DUPLICADO", ex.Codigo);
            Assert.Equal("codigoLote", Assert.Single(ex.Detalhes!).Key);
        }
    }
}
