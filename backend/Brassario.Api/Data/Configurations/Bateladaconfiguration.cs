using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Bateladaconfiguration: IEntityTypeConfiguration<Batelada>
    {
        public void Configure(EntityTypeBuilder<Batelada> builder)
        {
            builder.ToTable("batelada", t =>
            {
                t.HasCheckConstraint("ck_batelada_status", "status IN ('EM_ANDAMENTO','FINALIZADA','CANCELADA')");
                t.HasCheckConstraint("ck_batelada_volume_planejado", "volume_planejado > 0");
                t.HasCheckConstraint("ck_batelada_volume_final", "volume_final IS NULL OR volume_final > 0");
                t.HasCheckConstraint("ck_batelada_custo", "custo_total >= 0");
                // Motivo e data do cancelamento só existem em produção cancelada
                t.HasCheckConstraint("ck_batelada_cancelamento", "status = 'CANCELADA' OR (motivo_cancelamento IS NULL AND cancelado_em IS NULL)");
            });

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Status).HasMaxLength(15);

            // UNIQUE: código da batelada
            builder.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("uq_batelada_codigo");
            builder.HasIndex(x => x.DataProducao).HasDatabaseName("ix_batelada_data_producao");

            builder.HasOne(x => x.Receita)
                .WithMany(r => r.Bateladas)
                .HasForeignKey(x => x.ReceitaId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Usuario)
                .WithMany(u => u.Bateladas)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
