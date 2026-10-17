using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Refreshtokenconfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("refresh_token");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_refresh_token_hash");

            // CASCATA: ao apagar o usuário, os tokens dele também são apagados
            builder.HasOne(x => x.Usuario)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UsuarioId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
