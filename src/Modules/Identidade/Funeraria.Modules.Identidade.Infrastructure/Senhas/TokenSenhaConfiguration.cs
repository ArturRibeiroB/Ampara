using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Infrastructure.Database;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Identidade.Infrastructure.Senhas;

internal sealed class TokenSenhaConfiguration : IEntityTypeConfiguration<TokenSenha>
{
    public void Configure(EntityTypeBuilder<TokenSenha> builder)
    {
        builder.ToTable("tokens_senha");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id_token_senha").ValueGeneratedNever();
        builder.Property(t => t.Finalidade).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsFixedLength();

        builder.HasOne<Usuario>().WithMany().HasForeignKey(t => t.IdUsuario).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TokenHash).IsUnique();
    }
}

internal sealed class TokenSenhaRepository(IdentidadeDbContext context) : ITokenSenhaRepository
{
    public Task<TokenSenha?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        context.Set<TokenSenha>().FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public void Adicionar(TokenSenha token) => context.Set<TokenSenha>().Add(token);
}
