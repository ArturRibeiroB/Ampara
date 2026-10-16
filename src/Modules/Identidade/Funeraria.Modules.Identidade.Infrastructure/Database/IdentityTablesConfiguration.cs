using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Identidade.Infrastructure.Database;

/// <summary>Nomes em português para as tabelas do Identity (o padrão seria asp_net_users etc.).</summary>
internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.Property(u => u.Id).HasColumnName("id_usuario");
        builder.Property(u => u.Nome).HasMaxLength(150);

        // E-mail único na plataforma (DEC-02, AUT-01). O Identity cria o índice, mas não único.
        builder.HasIndex(u => u.NormalizedEmail).IsUnique();
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder) => builder.ToTable("roles");
}

internal sealed class UsuarioRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder) => builder.ToTable("usuario_roles");
}

internal sealed class UsuarioClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("usuario_claims");
}

internal sealed class UsuarioLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("usuario_logins");
}

internal sealed class UsuarioTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("usuario_tokens");
}

internal sealed class RoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) => builder.ToTable("role_claims");
}
