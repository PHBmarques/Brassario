using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Movimentacaoestoqueconfiguration: IEntityTypeConfiguration<MovimentacaoEstoque>
    {
        public void Configure(EntityTypeBuilder<MovimentacaoEstoque> builder)
        {
            builder.ToTable("movimentacao_estoque", t =>
            {
                t.HasCheckConstraint("ck_movimentacao_tipo", "tipo IN ('ENTRADA','SAIDA_PRODUCAO','AJUSTE','PERDA','ESTORNO')");
                // AJUSTE e PERDA exigem motivo (mínimo de 5 caracteres)
                t.HasCheckConstraint("ck_movimentacao_motivo",
                    "tipo NOT IN ('AJUSTE','PERDA') OR (motivo IS NOT NULL AND char_length(btrim(motivo)) >= 5)");
            });

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Tipo).HasMaxLength(15);

            builder.HasIndex(x => new { x.LoteInsumoId, x.DataHora })
                .HasDatabaseName("ix_movimentacao_lote_data");

            builder.HasOne(x => x.LoteInsumo)
                .WithMany(l => l.Movimentacoes)
                .HasForeignKey(x => x.LoteInsumoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Usuario)
                .WithMany(u => u.Movimentacoes)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Batelada)
                .WithMany(b => b.Movimentacoes)
                .HasForeignKey(x => x.BateladaId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
