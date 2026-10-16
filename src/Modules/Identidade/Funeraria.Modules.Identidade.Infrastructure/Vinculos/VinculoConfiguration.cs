using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Identidade.Infrastructure.Vinculos;

internal sealed class VinculoConfiguration : IEntityTypeConfiguration<UsuarioVinculo>
{
    public void Configure(EntityTypeBuilder<UsuarioVinculo> builder)
    {
        builder.ToTable("usuario_vinculos");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id_vinculo").ValueGeneratedNever();
        builder.Property(v => v.IdTenant).HasColumnName("id_tenant");
        builder.Property(v => v.Perfil).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.TipoTerceiro).HasMaxLength(30);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(v => v.IdUsuario).OnDelete(DeleteBehavior.Cascade);

        // Um vínculo por funerária: o login emite o token com o perfil desse vínculo (AUT-02).
        builder.HasIndex(v => new { v.IdUsuario, v.IdTenant }).IsUnique();
        builder.HasIndex(v => v.IdTenant);
    }
}
