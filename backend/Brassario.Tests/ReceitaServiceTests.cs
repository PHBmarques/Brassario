using Brassario.Api.DTOs.Request;
using Brassario.Api.Exceptions;
using Brassario.Api.Models;
using Brassario.Api.Repositories;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    public class ReceitaServiceTests
    {
        // Repositório falso em memória: testa as regras do serviço sem banco
        private class RepositorioFalso : IReceitaRepository
        {
            public List<Receita> Receitas { get; } = new();
            public List<Insumo> Insumos { get; } = new();

            public Task<List<Receita>> ListarAsync(string? busca, bool incluirInativas, CancellationToken ct = default)
            {
                var q = Receitas.AsEnumerable();
                if (!incluirInativas) q = q.Where(r => r.Ativa);
                if (!string.IsNullOrWhiteSpace(busca))
                {
                    var t = busca.Trim();
                    q = q.Where(r => r.Nome.Contains(t, StringComparison.OrdinalIgnoreCase)
                                  || (r.Estilo?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false));
                }
                return Task.FromResult(q.OrderBy(r => r.Nome).ToList());
            }

            public Task<Receita?> ObterPorIdAsync(long id, CancellationToken ct = default) =>
                Task.FromResult(Receitas.FirstOrDefault(r => r.Id == id));

            public Task<bool> ExisteNomeAsync(string nome, long? ignorarId, CancellationToken ct = default) =>
                Task.FromResult(Receitas.Any(r =>
                    string.Equals(r.Nome, nome.Trim(), StringComparison.OrdinalIgnoreCase) && r.Id != ignorarId));

            public Task<List<Insumo>> ObterInsumosAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default) =>
                Task.FromResult(Insumos.Where(i => ids.Contains(i.Id)).ToList());

            public Task AdicionarAsync(Receita receita, CancellationToken ct = default)
            {
                receita.Id = Receitas.Count + 1;
                Receitas.Add(receita);
                return Task.CompletedTask;
            }

            public Task SalvarAsync(CancellationToken ct = default) => Task.CompletedTask;
        }

        private static (ReceitaService Servico, RepositorioFalso Repo) Criar()
        {
            var repo = new RepositorioFalso();
            repo.Insumos.Add(new Insumo { Id = 1, Nome = "Malte Pilsen", UnidadeMedida = UnidadeMedida.KG, Ativo = true });
            repo.Insumos.Add(new Insumo { Id = 2, Nome = "Lúpulo Cascade", UnidadeMedida = UnidadeMedida.KG, Ativo = true });
            repo.Insumos.Add(new Insumo { Id = 3, Nome = "Levedura US-05", UnidadeMedida = UnidadeMedida.KG, Ativo = true });
            repo.Insumos.Add(new Insumo { Id = 4, Nome = "Malte Antigo", UnidadeMedida = UnidadeMedida.KG, Ativo = false });
            return (new ReceitaService(repo), repo);
        }

        // Sem itens informados, usa Malte Pilsen (18) e Lúpulo Cascade (0,3)
        private static ReceitaRequest Valida(string nome = "IPA Cascade", params (long Insumo, decimal Qtd)[] itens)
        {
            (long Insumo, decimal Qtd)[] lista = itens.Length > 0
                ? itens
                : new (long Insumo, decimal Qtd)[] { (1, 18m), (2, 0.3m) };

            return new ReceitaRequest
            {
                Nome = nome,
                Estilo = "India Pale Ale",
                VolumeBaseLitros = 100,
                ModoPreparo = "Mostura a 67 °C por 60 minutos.",
                Itens = lista
                    .Select(i => new ReceitaItemRequest { InsumoId = i.Insumo, Quantidade = i.Qtd })
                    .ToList()
            };
        }

        // ---------- Criar ----------

        [Fact]
        public async Task Cria_receita_valida_com_itens_ordenados_pelo_nome_do_insumo()
        {
            var (servico, _) = Criar();
            var criada = await servico.CriarAsync(Valida());

            Assert.Equal("IPA Cascade", criada.Nome);
            Assert.Equal(100m, criada.VolumeBaseLitros);
            Assert.True(criada.Ativa);
            Assert.Equal(2, criada.Itens.Count);
            Assert.Equal("Lúpulo Cascade", criada.Itens[0].InsumoNome);
            Assert.Equal("Malte Pilsen", criada.Itens[1].InsumoNome);
            Assert.Equal(18m, criada.Itens[1].Quantidade);
        }

        [Fact]
        public async Task Sem_itens_gera_erro_de_validacao_no_campo_itens()
        {
            var (servico, _) = Criar();
            var pedido = Valida();
            pedido.Itens = new List<ReceitaItemRequest>();

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(pedido));
            Assert.True(ex.Detalhes.ContainsKey("itens"));
        }

        [Fact]
        public async Task Insumo_repetido_na_mesma_receita_gera_erro_no_item_repetido()
        {
            var (servico, _) = Criar();
            var pedido = Valida("IPA", (1, 18m), (2, 0.3m), (1, 5m));

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(pedido));
            Assert.True(ex.Detalhes.ContainsKey("itens[2].insumoId"));
            Assert.False(ex.Detalhes.ContainsKey("itens[0].insumoId"));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-5.0)]
        [InlineData(0.001)]
        public async Task Volume_base_precisa_ser_maior_que_zero(double volume)
        {
            var (servico, _) = Criar();
            var pedido = Valida();
            pedido.VolumeBaseLitros = (decimal)volume;

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(pedido));
            Assert.True(ex.Detalhes.ContainsKey("volumeBaseLitros"));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public async Task Quantidade_do_item_precisa_ser_maior_que_zero(double quantidade)
        {
            var (servico, _) = Criar();
            var pedido = Valida("IPA", (1, 18m), (2, (decimal)quantidade));

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(pedido));
            Assert.True(ex.Detalhes.ContainsKey("itens[1].quantidade"));
        }

        [Fact]
        public async Task Varios_erros_voltam_juntos()
        {
            var (servico, _) = Criar();
            var pedido = Valida("   ", (1, 0m));
            pedido.VolumeBaseLitros = 0;

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(pedido));
            Assert.True(ex.Detalhes.ContainsKey("nome"));
            Assert.True(ex.Detalhes.ContainsKey("volumeBaseLitros"));
            Assert.True(ex.Detalhes.ContainsKey("itens[0].quantidade"));
        }

        [Fact]
        public async Task Insumo_inexistente_gera_erro_no_numero_do_ingrediente()
        {
            var (servico, _) = Criar();
            var pedido = Valida("IPA", (1, 18m), (999, 2m));

            var ex = await Assert.ThrowsAsync<ValidacaoException>(() => servico.CriarAsync(pedido));
            Assert.True(ex.Detalhes.ContainsKey("itens[1].insumoId"));
        }

        [Fact]
        public async Task Insumo_inativo_nao_entra_em_receita_nova()
        {
            var (servico, _) = Criar();
            var pedido = Valida("IPA", (1, 18m), (4, 2m));

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.CriarAsync(pedido));
            Assert.Equal("INSUMO_INATIVO", ex.Codigo);
            Assert.Contains("Malte Antigo", ex.Message);
        }

        [Fact]
        public async Task Nome_repetido_gera_conflito_sem_diferenciar_maiusculas()
        {
            var (servico, _) = Criar();
            await servico.CriarAsync(Valida("IPA Cascade"));

            await Assert.ThrowsAsync<ConflitoException>(() => servico.CriarAsync(Valida("  ipa cascade ")));
        }

        [Fact]
        public async Task Quantidade_e_volume_sao_arredondados_para_as_casas_do_banco()
        {
            var (servico, _) = Criar();
            var pedido = Valida("IPA", (1, 18.12349m));
            pedido.VolumeBaseLitros = 100.456m;

            var criada = await servico.CriarAsync(pedido);
            Assert.Equal(100.46m, criada.VolumeBaseLitros);
            Assert.Equal(18.123m, criada.Itens[0].Quantidade);
        }

        // ---------- Consultar ----------

        [Fact]
        public async Task Lista_so_as_ativas_por_padrao_e_conta_os_itens()
        {
            var (servico, _) = Criar();
            var a = await servico.CriarAsync(Valida("Witbier"));
            await servico.CriarAsync(Valida("IPA Cascade", (1, 18m), (2, 0.3m), (3, 0.2m)));
            await servico.AlterarStatusAsync(a.Id, false);

            var ativas = await servico.ListarAsync(null, false);
            var todas = await servico.ListarAsync(null, true);

            Assert.Single(ativas);
            Assert.Equal("IPA Cascade", ativas[0].Nome);
            Assert.Equal(3, ativas[0].QuantidadeItens);
            Assert.Equal(2, todas.Count);
        }

        [Fact]
        public async Task Busca_encontra_por_nome_ou_estilo()
        {
            var (servico, _) = Criar();
            await servico.CriarAsync(Valida("Pilsen Clássica"));
            var ipa = Valida("Sessão"); ipa.Estilo = "IPA";
            await servico.CriarAsync(ipa);

            Assert.Single(await servico.ListarAsync("pils", false));
            Assert.Single(await servico.ListarAsync("ipa", false));
            Assert.Empty(await servico.ListarAsync("stout", false));
        }

        [Fact]
        public async Task Obter_receita_inexistente_gera_404()
        {
            var (servico, _) = Criar();
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterAsync(99));
        }

        // ---------- Editar ----------

        [Fact]
        public async Task Edita_dados_troca_quantidade_remove_e_adiciona_ingredientes()
        {
            var (servico, _) = Criar();
            var criada = await servico.CriarAsync(Valida("IPA", (1, 18m), (2, 0.3m)));

            var pedido = Valida("IPA Nova", (1, 20m), (3, 0.5m)); // mantém 1 (nova qtd), tira 2, entra 3
            pedido.VolumeBaseLitros = 120;

            var editada = await servico.AtualizarAsync(criada.Id, pedido);

            Assert.Equal("IPA Nova", editada.Nome);
            Assert.Equal(120m, editada.VolumeBaseLitros);
            Assert.Equal(2, editada.Itens.Count);
            Assert.Equal(20m, editada.Itens.Single(i => i.InsumoId == 1).Quantidade);
            Assert.Equal(0.5m, editada.Itens.Single(i => i.InsumoId == 3).Quantidade);
            Assert.DoesNotContain(editada.Itens, i => i.InsumoId == 2);
        }

        [Fact]
        public async Task Editar_mantendo_o_proprio_nome_nao_gera_conflito()
        {
            var (servico, _) = Criar();
            var criada = await servico.CriarAsync(Valida("IPA Cascade"));

            var editada = await servico.AtualizarAsync(criada.Id, Valida("IPA Cascade"));
            Assert.Equal(criada.Id, editada.Id);
        }

        [Fact]
        public async Task Editar_para_o_nome_de_outra_receita_gera_conflito()
        {
            var (servico, _) = Criar();
            await servico.CriarAsync(Valida("Witbier"));
            var ipa = await servico.CriarAsync(Valida("IPA"));

            await Assert.ThrowsAsync<ConflitoException>(() => servico.AtualizarAsync(ipa.Id, Valida("witbier")));
        }

        [Fact]
        public async Task Editar_receita_inexistente_gera_404()
        {
            var (servico, _) = Criar();
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.AtualizarAsync(99, Valida()));
        }

        [Fact]
        public async Task Editar_sem_itens_ou_com_repeticao_continua_invalido()
        {
            var (servico, _) = Criar();
            var criada = await servico.CriarAsync(Valida());

            var semItens = Valida(); semItens.Itens = new List<ReceitaItemRequest>();
            await Assert.ThrowsAsync<ValidacaoException>(() => servico.AtualizarAsync(criada.Id, semItens));
            await Assert.ThrowsAsync<ValidacaoException>(() =>
                servico.AtualizarAsync(criada.Id, Valida("IPA Cascade", (1, 1m), (1, 2m))));
        }

        [Fact]
        public async Task Ingrediente_que_ja_estava_na_receita_continua_valido_se_o_insumo_for_inativado()
        {
            var (servico, repo) = Criar();
            var criada = await servico.CriarAsync(Valida("IPA", (1, 18m), (2, 0.3m)));
            repo.Insumos.Single(i => i.Id == 2).Ativo = false; // inativado depois de entrar na receita

            var editada = await servico.AtualizarAsync(criada.Id, Valida("IPA", (1, 19m), (2, 0.4m)));

            Assert.Equal(0.4m, editada.Itens.Single(i => i.InsumoId == 2).Quantidade);
            Assert.False(editada.Itens.Single(i => i.InsumoId == 2).InsumoAtivo);
        }

        [Fact]
        public async Task Ingrediente_novo_de_insumo_inativo_e_bloqueado_na_edicao()
        {
            var (servico, _) = Criar();
            var criada = await servico.CriarAsync(Valida("IPA", (1, 18m)));

            var ex = await Assert.ThrowsAsync<NegocioException>(() =>
                servico.AtualizarAsync(criada.Id, Valida("IPA", (1, 18m), (4, 1m))));
            Assert.Equal("INSUMO_INATIVO", ex.Codigo);
        }

        // ---------- Status e produção ----------

        [Fact]
        public async Task Alterar_status_inativa_e_reativa_a_receita()
        {
            var (servico, _) = Criar();
            var criada = await servico.CriarAsync(Valida());

            Assert.False((await servico.AlterarStatusAsync(criada.Id, false)).Ativa);
            Assert.True((await servico.AlterarStatusAsync(criada.Id, true)).Ativa);
        }

        [Fact]
        public async Task Receita_inativa_nao_pode_ser_produzida()
        {
            var (servico, _) = Criar();
            var criada = await servico.CriarAsync(Valida());
            await servico.AlterarStatusAsync(criada.Id, false);

            var ex = await Assert.ThrowsAsync<NegocioException>(() => servico.ObterParaProducaoAsync(criada.Id));
            Assert.Equal("STATUS_INVALIDO", ex.Codigo);
        }

        [Fact]
        public async Task Receita_ativa_e_devolvida_com_itens_para_a_producao()
        {
            var (servico, _) = Criar();
            var criada = await servico.CriarAsync(Valida());

            var receita = await servico.ObterParaProducaoAsync(criada.Id);

            Assert.Equal(criada.Id, receita.Id);
            Assert.Equal(2, receita.Itens.Count);
        }

        [Fact]
        public async Task Producao_de_receita_inexistente_gera_404()
        {
            var (servico, _) = Criar();
            await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => servico.ObterParaProducaoAsync(99));
        }
    }
}
