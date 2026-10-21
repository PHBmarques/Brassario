using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Tokenusuarioconfiguration : IEntityTypeConfiguration<TokenUsuario>
    {
        public void Configure(EntityTypeBuilder<TokenUsuario> builder)
        {
            builder.ToTable("token_usuario", t =>
            {
                t.HasCheckConstraint("ck_token_usuario_tipo",
                    "tipo IN ('CONFIRMAR_EMAIL','REDEFINIR_SENHA')");
            });

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Tipo).HasMaxLength(30);
            builder.Property(x => x.TokenHash).HasColumnType("char(64)");

            builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_token_usuario_hash");
            builder.HasIndex(x => new { x.UsuarioId, x.Tipo }).HasDatabaseName("ix_token_usuario_usuario_tipo");

            builder.HasOne(x => x.Usuario)
                .WithMany(u => u.TokensUsuario)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}