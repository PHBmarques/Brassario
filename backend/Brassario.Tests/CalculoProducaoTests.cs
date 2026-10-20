using Brassario.Api.Models;
using Brassario.Api.Services;
using Xunit;

namespace Brassario.Tests
{
    public class CalculoProducaoTests
    {
        private static readonly DateOnly Hoje = new(2026, 10, 20);

        private static LoteInsumo Lote(long id, string codigo, int validadeEmDias, decimal saldo, decimal preco = 1m, int entradaHaDias = 30) => new()
        {
            Id = id,
            InsumoId = 1,
            CodigoLote = codigo,
            DataEntrada = Hoje.AddDays(-entradaHaDias),
            DataValidade = Hoje.AddDays(validadeEmDias),
            QuantidadeInicial = saldo,
            QuantidadeAtual = saldo,
            PrecoUnitario = preco
        };

        // ---------- FEFO: consome primeiro o que vence antes ----------

        [Fact]
        public void Dois_lotes_consome_primeiro_o_que_vence_antes()
        {
            var tarde = Lote(1, "TARDE", 200, 10m);   // cadastrado primeiro, vence depois
            var cedo = Lote(2, "CEDO", 30, 10m);

            var fila = CalculoProducao.FilaFefo(new[] { tarde, cedo }, Hoje);
            var alocado = CalculoProducao.Alocar(fila, 4m);

            var unico = Assert.Single(alocado);
            Assert.Equal("CEDO", unico.Lote.CodigoLote);
            Assert.Equal(4m, unico.Quantidade);
        }

        [Fact]
        public void A_ordem_de_entrada_dos_lotes_na_lista_nao_muda_o_resultado()
        {
            var a = Lote(1, "A", 10, 5m);
            var b = Lote(2, "B", 20, 5m);
            var c = Lote(3, "C", 30, 5m);

            var ordem1 = CalculoProducao.FilaFefo(new[] { a, b, c }, Hoje).Select(l => l.CodigoLote);
            var ordem2 = CalculoProducao.FilaFefo(new[] { c, a, b }, Hoje).Select(l => l.CodigoLote);
            var ordem3 = CalculoProducao.FilaFefo(new[] { b, c, a }, Hoje).Select(l => l.CodigoLote);

            Assert.Equal(new[] { "A", "B", "C" }, ordem1);
            Assert.Equal(ordem1, ordem2);
            Assert.Equal(ordem1, ordem3);
        }

        [Fact]
        public void Empate_de_validade_usa_quem_entrou_primeiro_e_depois_o_menor_id()
        {
            var novo = Lote(1, "NOVO", 40, 5m, entradaHaDias: 2);
            var antigo = Lote(2, "ANTIGO", 40, 5m, entradaHaDias: 20);
            var igualA = Lote(3, "IGUAL-A", 40, 5m, entradaHaDias: 20);

            var fila = CalculoProducao.FilaFefo(new[] { novo, igualA, antigo }, Hoje).Select(l => l.CodigoLote);

            Assert.Equal(new[] { "ANTIGO", "IGUAL-A", "NOVO" }, fila);
        }

        [Fact]
        public void Fila_ignora_lote_vencido_lote_zerado_e_mantem_o_que_vence_hoje()
        {
            var vencido = Lote(1, "VENCIDO", -1, 50m);
            var zerado = Lote(2, "ZERADO", 10, 0m);
            var hoje = Lote(3, "HOJE", 0, 5m);
            var ok = Lote(4, "OK", 60, 5m);

            var fila = CalculoProducao.FilaFefo(new[] { vencido, zerado, ok, hoje }, Hoje).Select(l => l.CodigoLote);

            Assert.Equal(new[] { "HOJE", "OK" }, fila);
        }

        // ---------- Consumo dividido entre lotes ----------

        [Fact]
        public void Consumo_e_dividido_quando_o_primeiro_lote_nao_basta()
        {
            var a = Lote(1, "A", 10, 2m);
            var b = Lote(2, "B", 20, 5m);

            var alocado = CalculoProducao.Alocar(CalculoProducao.FilaFefo(new[] { b, a }, Hoje), 3m);

            Assert.Equal(2, alocado.Count);
            Assert.Equal(("A", 2m), (alocado[0].Lote.CodigoLote, alocado[0].Quantidade));   // esvazia o que vence antes
            Assert.Equal(("B", 1m), (alocado[1].Lote.CodigoLote, alocado[1].Quantidade));   // e completa com o próximo
        }

        [Fact]
        public void Consumo_divide_entre_tres_lotes_e_a_soma_fecha_com_o_necessario()
        {
            var fila = CalculoProducao.FilaFefo(new[]
            {
            Lote(1, "A", 10, 1.5m), Lote(2, "B", 20, 2.25m), Lote(3, "C", 30, 10m)
        }, Hoje);

            var alocado = CalculoProducao.Alocar(fila, 5m);

            Assert.Equal(new[] { 1.5m, 2.25m, 1.25m }, alocado.Select(a => a.Quantidade).ToArray());
            Assert.Equal(5m, alocado.Sum(a => a.Quantidade));
        }

        [Fact]
        public void Quando_o_lote_cobre_exatamente_o_necessario_nao_toca_no_proximo()
        {
            var fila = CalculoProducao.FilaFefo(new[] { Lote(1, "A", 10, 4m), Lote(2, "B", 20, 9m) }, Hoje);

            var alocado = CalculoProducao.Alocar(fila, 4m);

            var unico = Assert.Single(alocado);
            Assert.Equal("A", unico.Lote.CodigoLote);
        }

        [Fact]
        public void Quando_a_fila_nao_basta_devolve_so_o_que_existe()
        {
            var fila = CalculoProducao.FilaFefo(new[] { Lote(1, "A", 10, 2m), Lote(2, "B", 20, 1m) }, Hoje);

            var alocado = CalculoProducao.Alocar(fila, 10m);

            Assert.Equal(3m, alocado.Sum(a => a.Quantidade));   // a falta é calculada por quem chama
        }

        [Fact]
        public void Alocar_nao_altera_o_saldo_dos_lotes()
        {
            var a = Lote(1, "A", 10, 2m);
            var b = Lote(2, "B", 20, 5m);

            CalculoProducao.Alocar(CalculoProducao.FilaFefo(new[] { a, b }, Hoje), 3m);

            Assert.Equal(2m, a.QuantidadeAtual);
            Assert.Equal(5m, b.QuantidadeAtual);
        }

        // ---------- Escala pelo volume (RN-04) ----------

        [Theory]
        [InlineData(18, 100, 100, 18)]        // mesmo volume da receita
        [InlineData(18, 100, 50, 9)]          // metade
        [InlineData(18, 100, 200, 36)]        // dobro
        [InlineData(0.3, 100, 50, 0.15)]
        [InlineData(18, 100, 50.5, 9.09)]     // volume com casa decimal
        [InlineData(2, 20, 5, 0.5)]           // receita base de 20 L
        public void Escala_a_quantidade_pelo_volume(double baseQtd, double volumeBase, double volume, double esperado)
        {
            var r = CalculoProducao.Escalar((decimal)baseQtd, (decimal)volumeBase, (decimal)volume);

            Assert.Equal((decimal)esperado, r);
        }

        [Fact]
        public void Escala_arredonda_para_tres_casas()
        {
            Assert.Equal(0.333m, CalculoProducao.Escalar(1m, 3m, 1m));
            Assert.Equal(0.667m, CalculoProducao.Escalar(2m, 3m, 1m));
            Assert.Equal(0.099m, CalculoProducao.Escalar(0.3m, 100m, 33m));
        }

        [Fact]
        public void Escala_e_proporcional_dobrar_o_volume_dobra_todas_as_quantidades()
        {
            foreach (var qtd in new[] { 18m, 0.3m, 1.25m, 7m })
            {
                var simples = CalculoProducao.Escalar(qtd, 100m, 80m);
                var dobro = CalculoProducao.Escalar(qtd, 100m, 160m);
                Assert.Equal(simples * 2, dobro);
            }
        }

        // ---------- Custo com preços diferentes por lote ----------

        [Fact]
        public void Custo_do_consumo_e_quantidade_vezes_o_preco_do_lote()
        {
            Assert.Equal(85m, CalculoProducao.Custo(10m, 8.5m));
            Assert.Equal(73.6m, CalculoProducao.Custo(8m, 9.2m));
            Assert.Equal(36m, CalculoProducao.Custo(0.3m, 120m));
        }

        [Fact]
        public void Consumo_dividido_entre_lotes_de_precos_diferentes_soma_cada_parte_pelo_preco_do_seu_lote()
        {
            var barato = Lote(1, "BARATO", 10, 10m, preco: 8.5m);
            var caro = Lote(2, "CARO", 20, 40m, preco: 9.2m);

            var alocado = CalculoProducao.Alocar(CalculoProducao.FilaFefo(new[] { caro, barato }, Hoje), 18m);
            var custos = alocado.Select(a => CalculoProducao.Custo(a.Quantidade, a.Lote.PrecoUnitario)).ToList();

            Assert.Equal(new[] { 85m, 73.6m }, custos.ToArray());    // 10 x 8,5 e 8 x 9,2
            Assert.Equal(158.6m, CalculoProducao.CustoTotal(custos));
            // um preço único (o do segundo lote) daria 165,6: o custo por lote é o certo
            Assert.NotEqual(18m * 9.2m, CalculoProducao.CustoTotal(custos));
        }

        [Fact]
        public void Custo_total_arredonda_para_duas_casas()
        {
            Assert.Equal(0.03m, CalculoProducao.CustoTotal(new[] { 0.0149m, 0.0149m }));   // 0,0298
            Assert.Equal(0.01m, CalculoProducao.CustoTotal(new[] { 0.005m }));            // meio centavo sobe
        }
    }
}
