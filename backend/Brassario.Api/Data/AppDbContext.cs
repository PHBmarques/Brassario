using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Brassario.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios => Set<Usuario>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
        public DbSet<Insumo> Insumos => Set<Insumo>();
        public DbSet<LoteInsumo> LotesInsumo => Set<LoteInsumo>();
        public DbSet<Receita> Receitas => Set<Receita>();
        public DbSet<ReceitaItem> ReceitaItens => Set<ReceitaItem>();
        public DbSet<Batelada> Bateladas => Set<Batelada>();
        public DbSet<BateladaConsumo> BateladaConsumos => Set<BateladaConsumo>();
        public DbSet<MovimentacaoEstoque> Movimentacoes => Set<MovimentacaoEstoque>();

        public DbSet<TokenUsuario> TokensUsuario => Set<TokenUsuario>();

        // Grava todos os enums como texto (ex.: "ADMIN"), de acordo com os CHECK da modelagem
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<System.Enum>().HaveConversion<string>();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // BD-2: as chaves, os índices, os UNIQUE e os CHECK ficam em classes IEntityTypeConfiguration
            // dentro de Data/Configurations. Esta linha carrega todas elas automaticamente.
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    property.SetColumnName(ParaSnakeCase(property.Name));
                }
            }
        }
    private static string ParaSnakeCase(string nome) =>
            Regex.Replace(nome, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
    }
}
