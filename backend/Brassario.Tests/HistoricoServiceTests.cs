using Brassario.Api.DTOs.Response;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    /// <summary>API-22: regras do histórico de produções e de movimentações (validação, período, paginação, formato).</summary>
    public class HistoricoServiceTests
    {
        // Brasília, UTC-3 fixo: não depende do fuso da máquina que roda os testes
        private static readonly TimeZoneInfo Brasilia = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");

        private class RepoFalso : IHistoricoRepository
        {
            public List<Batelada> Bateladas { get; set; } = new();
            public List<MovimentacaoEstoque> Movimentacoes { get; set; } = new();
            public int Total { get; set; }

            public FiltroBateladas? UltimoFiltroBateladas { get; private set; }
            public FiltroMovimentacoes? UltimoFiltroMovimentacoes { get; private set; }
            public (int Pagina, int Tamanho) UltimaPagina { get; private set; }
            public int Chamadas { get; private set; }

            public Task<(List<Batelada> Itens, int Total)> ListarBateladasAsync(FiltroBateladas filtro, int pagina, int tamanho, CancellationToken ct = default)
            {
                Chamadas++;
                UltimoFiltroBateladas = filtro;
                UltimaPagina = (pagina, tamanho);
                return Task.FromResult((Bateladas, Total));
            }

            public Task<(List<MovimentacaoEstoque> Itens, int Total)> ListarMovimentacoesAsync(FiltroMovimentacoes filtro, int pagina, int tamanho, CancellationToken ct = default)
            {
                Chamadas++;
                UltimoFiltroMovimentacoes = filtro;
                UltimaPagina = (pagina, tamanho);
                return Task.FromResult((Movimentacoes, Total));
            }
        }

        private static (HistoricoService Servico, RepoFalso Repo) Criar()
        {
            var repo = new RepoFalso();
            return (new HistoricoService(repo, Brasilia), repo);
        }

        // ---------- Produções ----------

        [Fact]
        public async Task Sem_parametros_usa_pagina_1_com_20_itens_e_nenhum_filtro()
        {
            var (servico, repo) = Criar();

            var r = await servico.ListarBateladasAsync(null, null, null, null, null, null);

            Assert.Equal((1, 20), repo.UltimaPagina);
            Assert.Equal(new FiltroBateladas(null, null, null, null), repo.UltimoFiltroBateladas);
            Assert.Equal((1, 20), (r.Pagina, r.TamanhoPagina));
        }

        [Fact]
        public async Task Periodo_e_convertido_do_dia_local_para_UTC_com_fim_no_inicio_do_dia_seguinte()
        {
            var (servico, repo) = Criar();

            await servico.ListarBateladasAsync(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22), null, null, null, null);

            // 20/10 00:00 em Brasília = 03:00 UTC; o fim é 23/10 00:00 em Brasília (exclusivo)
            Assert.Equal(new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc), repo.UltimoFiltroBateladas!.InicioUtc);
            Assert.Equal(new DateTime(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc), repo.UltimoFiltroBateladas.FimUtcExclusivo);
        }

        [Fact]
        public async Task So_a_data_inicial_ou_so_a_final_deixa_o_outro_lado_aberto()
        {
            var (servico, repo) = Criar();

            await servico.ListarBateladasAsync(new DateOnly(2026, 10, 20), null, null, null, null, null);
            Assert.NotNull(repo.UltimoFiltroBateladas!.InicioUtc);
            Assert.Null(repo.UltimoFiltroBateladas.FimUtcExclusivo);

            await servico.ListarBateladasAsync(null, new DateOnly(2026, 10, 20), null, null, null, null);
            Assert.Null(repo.UltimoFiltroBateladas!.InicioUtc);
            Assert.NotNull(repo.UltimoFiltroBateladas.FimUtcExclusivo);
        }

        [Fact]
        public async Task Receita_e_status_vao_para_o_filtro_e_o_status_aceita_minusculas()
        {
            var (servico, repo) = Criar();

            await servico.ListarBateladasAsync(null, null, 5, " finalizada ", null, null);

            Assert.Equal(5, repo.UltimoFiltroBateladas!.ReceitaId);
            Assert.Equal(StatusBatelada.FINALIZADA, repo.UltimoFiltroBateladas.Status);
        }

        [Fact]
        public async Task Periodo_invertido_gera_400_sem_consultar_o_banco()
        {
            var (servico, repo) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.ListarBateladasAsync(new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 20), null, null, null, null));

            Assert.True(ex.Detalhes.ContainsKey("de"));
            Assert.Equal(0, repo.Chamadas);
        }

        [Theory]
        [InlineData("CONCLUIDA")]
        [InlineData("123")]
        public async Task Status_invalido_gera_400_listando_os_validos(string status)
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.ListarBateladasAsync(null, null, null, status, null, null));

            Assert.Contains("EM_ANDAMENTO", ex.Detalhes["status"][0]);
        }

        [Theory]
        [InlineData(0, null, "pagina")]
        [InlineData(-1, null, "pagina")]
        [InlineData(null, 0, "tamanho")]
        [InlineData(null, 101, "tamanho")]
        [InlineData(null, -5, "tamanho")]
        public async Task Pagina_ou_tamanho_fora_do_limite_gera_400(int? pagina, int? tamanho, string campo)
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.ListarBateladasAsync(null, null, null, null, pagina, tamanho));

            Assert.True(ex.Detalhes.ContainsKey(campo));
        }

        [Fact]
        public async Task Receita_com_id_invalido_gera_400()
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.ListarBateladasAsync(null, null, 0, null, null, null));

            Assert.True(ex.Detalhes.ContainsKey("receitaId"));
        }

        [Fact]
        public async Task Varios_erros_voltam_juntos_no_mesmo_400()
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.ListarBateladasAsync(new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 20), -1, "X", 0, 0));

            Assert.Equal(5, ex.Detalhes.Count);
        }

        [Theory]
        [InlineData(0, 20, 0)]
        [InlineData(1, 20, 1)]
        [InlineData(20, 20, 1)]
        [InlineData(21, 20, 2)]
        [InlineData(45, 20, 3)]
        [InlineData(5, 2, 3)]
        public async Task Total_de_paginas_e_arredondado_para_cima(int total, int tamanho, int totalPaginas)
        {
            var (servico, repo) = Criar();
            repo.Total = total;

            var r = await servico.ListarBateladasAsync(null, null, null, null, 1, tamanho);

            Assert.Equal(total, r.Total);
            Assert.Equal(totalPaginas, r.TotalPaginas);
        }

        [Fact]
        public async Task Cada_producao_vem_em_uma_linha_com_codigo_receita_quantidade_custo_responsavel_e_situacao()
        {
            var (servico, repo) = Criar();
            var data = new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
            repo.Bateladas = new List<Batelada>
            {
                new()
                {
                    Id = 12, Codigo = "B-2026-0012", ReceitaId = 3, Receita = new Receita { Id = 3, Nome = "IPA Cascade" },
                    UsuarioId = 7, Usuario = new Usuario { Id = 7, Nome = "Ana Souza" },
                    DataProducao = data, VolumePlanejado = 100m, CustoTotal = 189.5m, Status = StatusBatelada.EM_ANDAMENTO
                },
                new()
                {
                    Id = 11, Codigo = "B-2026-0011", ReceitaId = 4, Receita = new Receita { Id = 4, Nome = "Pilsen" },
                    UsuarioId = 8, Usuario = new Usuario { Id = 8, Nome = "Bruno Lima" },
                    DataProducao = data.AddDays(-1), VolumePlanejado = 100m, VolumeFinal = 92.5m, CustoTotal = 150m, Status = StatusBatelada.FINALIZADA
                }
            };
            repo.Total = 2;

            var r = await servico.ListarBateladasAsync(null, null, null, null, null, null);

            Assert.Equal(new BateladaResumoResponse(12, "B-2026-0012", 3, "IPA Cascade", data, 100m, 189.5m, 7, "Ana Souza", StatusBatelada.EM_ANDAMENTO), r.Itens[0]);
            Assert.Equal(92.5m, r.Itens[1].Quantidade);   // com volume final, vale o final
            Assert.Equal(StatusBatelada.FINALIZADA, r.Itens[1].Status);
        }

        [Fact]
        public void A_linha_da_producao_nao_tem_os_lotes_consumidos()
        {
            var nomes = typeof(BateladaResumoResponse).GetProperties().Select(p => p.Name).ToList();

            Assert.DoesNotContain(nomes, n => n.Contains("Consumo") || n.Contains("Lote"));
        }

        // ---------- Movimentações ----------

        [Fact]
        public async Task Movimentacoes_repassam_os_tres_filtros_e_a_pagina()
        {
            var (servico, repo) = Criar();

            await servico.ListarMovimentacoesAsync(1, 2, 3, 4, 50);

            Assert.Equal(new FiltroMovimentacoes(1, 2, 3), repo.UltimoFiltroMovimentacoes);
            Assert.Equal((4, 50), repo.UltimaPagina);
        }

        [Theory]
        [InlineData(0L, null, null, "insumoId")]
        [InlineData(null, -1L, null, "loteId")]
        [InlineData(null, null, 0L, "producaoId")]
        public async Task Movimentacoes_com_id_invalido_geram_400(long? insumo, long? lote, long? producao, string campo)
        {
            var (servico, _) = Criar();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.ListarMovimentacoesAsync(insumo, lote, producao, null, null));

            Assert.True(ex.Detalhes.ContainsKey(campo));
        }

        [Fact]
        public async Task Movimentacao_de_saida_e_de_entrada_vem_com_lote_insumo_producao_e_responsavel()
        {
            var (servico, repo) = Criar();
            var malte = new Insumo { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG };
            var lote = new LoteInsumo { Id = 9, InsumoId = 1, Insumo = malte, CodigoLote = "M-1" };
            var data = new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
            repo.Movimentacoes = new List<MovimentacaoEstoque>
            {
                new()
                {
                    Id = 2, LoteInsumoId = 9, LoteInsumo = lote, UsuarioId = 7, Usuario = new Usuario { Id = 7, Nome = "Ana Souza" },
                    BateladaId = 12, Batelada = new Batelada { Id = 12, Codigo = "B-2026-0012" },
                    Tipo = TipoMovimentacao.SAIDA_PRODUCAO, Quantidade = -18m, DataHora = data
                },
                new()
                {
                    Id = 1, LoteInsumoId = 9, LoteInsumo = lote, UsuarioId = 8, Usuario = new Usuario { Id = 8, Nome = "Bruno Lima" },
                    Tipo = TipoMovimentacao.ENTRADA, Quantidade = 100m, DataHora = data.AddDays(-5)
                }
            };
            repo.Total = 2;

            var r = await servico.ListarMovimentacoesAsync(null, 9, null, null, null);

            Assert.Equal(new MovimentacaoResponse(2, data, TipoMovimentacao.SAIDA_PRODUCAO, -18m, null, 1, "Malte Pilsen", UnidadeMedida.KG, 9, "M-1", 12, "B-2026-0012", 7, "Ana Souza"), r.Itens[0]);
            Assert.Null(r.Itens[1].ProducaoId);       // entrada não tem produção
            Assert.Null(r.Itens[1].CodigoProducao);
            Assert.Equal(100m, r.Itens[1].Quantidade);
        }

        // ---------- Período local ----------

        [Fact]
        public void Producao_as_23h30_do_dia_20_em_Brasilia_cai_no_dia_20_mesmo_sendo_dia_21_em_UTC()
        {
            var (inicio, fim) = PeriodoLocal.ParaUtc(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 20), Brasilia);
            var producao = new DateTime(2026, 10, 21, 2, 30, 0, DateTimeKind.Utc);   // 20/10 23:30 em Brasília

            Assert.True(producao >= inicio && producao < fim);
        }

        [Fact]
        public void Sem_datas_o_periodo_fica_aberto()
        {
            var (inicio, fim) = PeriodoLocal.ParaUtc(null, null, Brasilia);

            Assert.Null(inicio);
            Assert.Null(fim);
        }

        [Fact]
        public void O_servico_pode_ser_criado_com_o_fuso_do_servidor()
        {
            var servico = new HistoricoService(new RepoFalso());
            Assert.NotNull(servico);
        }
    }
}
