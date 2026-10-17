using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Insumoconfiguration: IEntityTypeConfiguration<Insumo>
    {
        public void Configure(EntityTypeBuilder<Insumo> builder)
        {
            builder.ToTable("insumo", t =>
            {
                t.HasCheckConstraint("ck_insumo_categoria", "categoria IN ('MALTE','LUPULO','LEVEDURA','ADJUNTO','OUTRO')");
                t.HasCheckConstraint("ck_insumo_unidade", "unidade_medida IN ('KG','G','L','ML','UN')");
                t.HasCheckConstraint("ck_insumo_estoque_minimo", "estoque_minimo >= 0");
                t.HasCheckConstraint("ck_insumo_dias_critica", "dias_validade_critica >= 0");
                t.HasCheckConstraint("ck_insumo_dias_atencao", "dias_validade_atencao >= dias_validade_critica");
            });

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Categoria).HasMaxLength(15);
            builder.Property(x => x.UnidadeMedida).HasMaxLength(5);

            // UNIQUE: nome do insumo
            builder.HasIndex(x => x.Nome).IsUnique().HasDatabaseName("uq_insumo_nome");
        }
    }
}
