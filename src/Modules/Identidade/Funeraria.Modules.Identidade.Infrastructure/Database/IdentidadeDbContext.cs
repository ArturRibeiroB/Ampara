using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Identidade.Infrastructure.Database;

/// <summary>
/// DbContext da Identidade. Herda de <see cref="IdentityDbContext{TUser,TRole,TKey}"/> e por isso não usa a base
/// <see cref="ModuleDbContext"/>: aplica as mesmas convenções pelas extensões do <see cref="ModelBuilder"/> (INF-03).
/// Não tem filtro de tenant: as contas são da plataforma e os vínculos são lidos por usuário.
/// </summary>
public sealed class IdentidadeDbContext(DbContextOptions<IdentidadeDbContext> options)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(options), IModuleDbContext, IIdentidadeUnitOfWork
{
    public const string SchemaName = "identidade";

    public string Schema => SchemaName;

    internal DbSet<UsuarioVinculo> Vinculos => Set<UsuarioVinculo>();

    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentidadeDbContext).Assembly);
        modelBuilder.ApplyOutbox();
        modelBuilder.ApplyConcurrencyTokens();
        modelBuilder.ApplyAuditProperties();
    }

    /// <summary>Violação de índice único vira <c>ViolacaoDeUnicidadeException</c>, como nos outros módulos.</summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) =>
        ErrosDeBanco.SalvarAsync(ct => base.SaveChangesAsync(acceptAllChangesOnSuccess, ct), cancellationToken);

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
    }
}
