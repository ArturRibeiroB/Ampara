using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Identidade.Infrastructure.Sessoes;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id_refresh_token").ValueGeneratedNever();
        builder.Property(t => t.IdTenant).HasColumnName("id_tenant");
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength();

        builder.HasOne<Usuario>().WithMany().HasForeignKey(t => t.IdUsuario).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => t.IdUsuario).HasFilter("revogado_em IS NULL");
    }
}
