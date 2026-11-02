using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Brassario.Api.Security;

namespace Brassario.Api.Data
{
    public static class DbSeeder
    {
        private record LoteSeed(string Codigo, Fornecedor Fornecedor, int EntradaHaDias, int ValidadeEmDias, decimal Qtd, decimal Preco);

        public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher, IConfiguration config, ILogger logger)
        {
            if (await db.Usuarios.AnyAsync() || await db.Insumos.AnyAsync())
            {
                logger.LogInformation("Seed ignorado: o banco já tem dados.");
                return;
            }

            var hoje = DateOnly.FromDateTime(DateTime.Today);

            var admin = new Usuario
            {
                Nome = "Administrador",
                Email = config["Seed:AdminEmail"] ?? "admin@brassario.com",
                SenhaHash = BCrypt.Net.BCrypt.HashPassword(config["Seed:AdminSenha"] ?? "Admin@2026"),
                Perfil = PerfilUsuario.ADMIN
            };
            db.Usuarios.Add(admin);

            // O OPERADOR só entra no seed se pedido (para cadastrá-lo ao vivo na apresentação, deixe false)
            if (config.GetValue<bool>("Seed:IncluirOperador"))
            {
                db.Usuarios.Add(new Usuario
                {
                    Nome = "Operador",
                    Email = config["Seed:OperadorEmail"] ?? "operador@brassario.com",
                    SenhaHash = BCrypt.Net.BCrypt.HashPassword(config["Seed:OperadorSenha"] ?? "Operador@2026"),
                    Perfil = PerfilUsuario.OPERADOR
                });
            }

            // Insumos, fornecedores, lotes e receita de exemplo só entram se pedido (Seed:IncluirDadosExemplo=true).
            // Por padrão o banco nasce só com os usuários, para cadastrar tudo ao vivo.
            if (!config.GetValue<bool>("Seed:IncluirDadosExemplo"))
            {
                await db.SaveChangesAsync();
                logger.LogInformation("Seed concluído: apenas usuário(s), sem insumos nem fornecedores.");
                return;
            }

            var hopBrasil = new Fornecedor { Nome = "Hop Brasil", Contato = "contato@hopbrasil.example" };
            var maltariaSul = new Fornecedor { Nome = "Maltaria Sul", Contato = "vendas@maltariasul.example" };
            db.Fornecedores.AddRange(hopBrasil, maltariaSul);

            Insumo NovoInsumo(string nome, CategoriaInsumo categoria, decimal estoqueMinimo, params LoteSeed[] lotes)
            {
                var insumo = new Insumo
                {
                    Nome = nome,
                    Categoria = categoria,
                    UnidadeMedida = UnidadeMedida.KG,
                    EstoqueMinimo = estoqueMinimo
                };
                db.Insumos.Add(insumo);

                foreach (var l in lotes)
                {
                    var lote = new LoteInsumo
                    {
                        Insumo = insumo,
                        Fornecedor = l.Fornecedor,
                        CodigoLote = l.Codigo,
                        DataEntrada = hoje.AddDays(-l.EntradaHaDias),
                        DataValidade = hoje.AddDays(l.ValidadeEmDias),
                        QuantidadeInicial = l.Qtd,
                        QuantidadeAtual = l.Qtd,
                        PrecoUnitario = l.Preco
                    };
                    db.LotesInsumo.Add(lote);

                    db.Movimentacoes.Add(new MovimentacaoEstoque
                    {
                        LoteInsumo = lote,
                        Usuario = admin,
                        Tipo = TipoMovimentacao.ENTRADA,
                        Quantidade = l.Qtd,
                        Motivo = "Estoque inicial (seed)"
                    });
                }
                return insumo;
            }

            var pilsen = NovoInsumo("Malte Pilsen", CategoriaInsumo.MALTE, 20,
                new("M-101", maltariaSul, 40, 90, 25m, 6.50m),
                new("M-102", maltariaSul, 10, 180, 50m, 6.80m));

            NovoInsumo("Malte Munich", CategoriaInsumo.MALTE, 10,
                new("M-201", maltariaSul, 30, 120, 15m, 7.20m),
                new("M-202", maltariaSul, 5, 200, 25m, 7.40m));

            // Um dos lotes já está VENCIDO (não entra no saldo nem na produção)
            NovoInsumo("Malte Caramelo", CategoriaInsumo.MALTE, 5,
                new("C-12", maltariaSul, 90, -5, 4m, 8.00m),
                new("C-13", maltariaSul, 8, 150, 10m, 8.20m));

            // Lote A vence em 25 dias (atenção). 0,55 kg no total: dá para 100 L, mas NÃO para 200 L (erro de estoque insuficiente)
            var cascade = NovoInsumo("Lúpulo Cascade", CategoriaInsumo.LUPULO, 0.5m,
                new("NF1234-A", hopBrasil, 35, 25, 0.150m, 180m),
                new("NF1301-B", hopBrasil, 7, 180, 0.400m, 185m));

            var citra = NovoInsumo("Lúpulo Citra", CategoriaInsumo.LUPULO, 0.3m,
                new("CT-01", hopBrasil, 20, 60, 0.500m, 210m),
                new("CT-02", hopBrasil, 4, 200, 1.000m, 215m));

            // Abaixo do estoque mínimo (0,5 kg < 1 kg): mostra o alerta de estoque baixo
            NovoInsumo("Lúpulo Saaz", CategoriaInsumo.LUPULO, 1.0m,
                new("SZ-01", hopBrasil, 25, 40, 0.200m, 170m),
                new("SZ-02", hopBrasil, 6, 100, 0.300m, 172m));

            // Lote L-88 vence em 6 dias: Validade Crítica (vermelho)
            var us05 = NovoInsumo("Levedura US-05", CategoriaInsumo.LEVEDURA, 0.2m,
                new("L-88", hopBrasil, 50, 6, 0.500m, 220m),
                new("L-91", hopBrasil, 3, 120, 1.000m, 225m));

            NovoInsumo("Levedura S-04", CategoriaInsumo.LEVEDURA, 0.2m,
                new("L-45", hopBrasil, 20, 45, 0.600m, 210m),
                new("L-46", hopBrasil, 2, 150, 1.000m, 215m));

            // ---------- Receita de exemplo ----------
            var receita = new Receita
            {
                Nome = "IPA Cascade",
                Estilo = "India Pale Ale",
                VolumeBaseLitros = 100m,
                ModoPreparo = "Mosturar a 67 °C por 60 minutos. Ferver por 60 minutos e adicionar os lúpulos conforme a tabela. Fermentar com US-05 a 19 °C."
            };
            receita.Itens.Add(new ReceitaItem { Insumo = pilsen, Quantidade = 18m });
            receita.Itens.Add(new ReceitaItem { Insumo = cascade, Quantidade = 0.300m });
            receita.Itens.Add(new ReceitaItem { Insumo = citra, Quantidade = 0.200m });
            receita.Itens.Add(new ReceitaItem { Insumo = us05, Quantidade = 0.200m });
            db.Receitas.Add(receita);

            // Tudo em uma única gravação (transação): ou entra tudo, ou nada
            await db.SaveChangesAsync();
            logger.LogInformation("Seed concluído: usuário(s), 8 insumos, 16 lotes e 1 receita.");
        }
    }
}
