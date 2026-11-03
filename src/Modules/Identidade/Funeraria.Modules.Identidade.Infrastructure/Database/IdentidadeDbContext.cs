using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Equipe;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Identidade.Infrastructure.Usuarios;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Funeraria.Modules.Identidade.Infrastructure.Database;

/// <summary>
/// DbContext da Identidade. Herda de <see cref="IdentityDbContext{TUser,TRole,TKey}"/> e por isso não usa a base
/// <see cref="ModuleDbContext"/>: aplica as mesmas convenções pelas extensões do <see cref="ModelBuilder"/> (INF-03).
/// Não tem filtro de tenant: as contas são da plataforma e os vínculos são lidos por usuário.
/// O CPF dos funcionários é gravado cifrado (SEG-01). Como a cifra é aleatória, unicidade e busca usam a coluna
/// <c>cpf_hash</c> (<see cref="CpfHash"/>), preenchida aqui ao salvar.
/// </summary>
public sealed class IdentidadeDbContext(DbContextOptions<IdentidadeDbContext> options, ICriptografiaDados criptografia)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(options), IModuleDbContext, IIdentidadeUnitOfWork, IDataProtectionKeyContext
{
    public const string SchemaName = "identidade";

    /// <summary>Propriedade de sombra com o HMAC do CPF do funcionário.</summary>
    public const string CpfHash = nameof(CpfHash);

    public string Schema => SchemaName;

    internal DbSet<UsuarioVinculo> Vinculos => Set<UsuarioVinculo>();

    internal DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Chaves do Data Protection, que cifram os dados pessoais em repouso (SEG-01). Ficam no banco, fora do código.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>Hash para buscar pelo CPF (já normalizado, só dígitos).</summary>
    internal string HashCpf(string cpf) => criptografia.Hash(cpf);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(SchemaName);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentidadeDbContext).Assembly);
        modelBuilder.Entity<DataProtectionKey>().ToTable("chaves_protecao_dados");
        modelBuilder.ApplyOutbox();
        modelBuilder.ApplyConcurrencyTokens();
        modelBuilder.ApplyAuditProperties();

        modelBuilder.Entity<Funcionario>().Property(f => f.Cpf).HasConversion(
            new ValueConverter<string, string>(v => criptografia.Cifrar(v), v => criptografia.Decifrar(v)));
    }

    /// <summary>Violação de índice único vira <c>ViolacaoDeUnicidadeException</c>, como nos outros módulos.</summary>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entrada in ChangeTracker.Entries<Funcionario>().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            entrada.Property(CpfHash).CurrentValue = criptografia.Hash(entrada.Entity.Cpf);
        }

        return ErrosDeBanco.SalvarAsync(ct => base.SaveChangesAsync(acceptAllChangesOnSuccess, ct), cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<decimal>().HavePrecision(12, 2);
    }
}
