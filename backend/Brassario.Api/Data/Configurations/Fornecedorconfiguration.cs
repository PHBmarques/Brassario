using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Fornecedorconfiguration: IEntityTypeConfiguration<Fornecedor>
    {
        public void Configure(EntityTypeBuilder<Fornecedor> builder)
        {
            builder.ToTable("fornecedor");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.Nome).IsUnique().HasDatabaseName("uq_fornecedor_nome");
        }
    }
}
