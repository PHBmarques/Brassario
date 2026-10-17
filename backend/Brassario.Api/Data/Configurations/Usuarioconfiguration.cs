using Brassario.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brassario.Api.Data.Configurations
{
    public class Usuarioconfiguration: IEntityTypeConfiguration<Usuario>
    {
        public void Configure(EntityTypeBuilder<Usuario> builder)
        {
            builder.ToTable("usuario", t =>
            {
                t.HasCheckConstraint("ck_usuario_perfil", "perfil IN ('ADMIN','OPERADOR')");
            });

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Perfil).HasMaxLength(10);

            // UNIQUE: e-mail
            builder.HasIndex(x => x.Email).IsUnique().HasDatabaseName("uq_usuario_email");
        }
    }
}
