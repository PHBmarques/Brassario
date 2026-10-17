using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Bateladaconsumoconfiguration: IEntityTypeConfiguration<BateladaConsumo>
    {
        public void Configure(EntityTypeBuilder<BateladaConsumo> builder)
        {
            builder.ToTable("batelada_consumo", t =>
            {
                t.HasCheckConstraint("ck_batelada_consumo_quantidade", "quantidade > 0");
                t.HasCheckConstraint("ck_batelada_consumo_custo", "custo >= 0");
            });

            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.Batelada)
                .WithMany(b => b.Consumos)
                .HasForeignKey(x => x.BateladaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.LoteInsumo)
                .WithMany(l => l.Consumos)
                .HasForeignKey(x => x.LoteInsumoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
