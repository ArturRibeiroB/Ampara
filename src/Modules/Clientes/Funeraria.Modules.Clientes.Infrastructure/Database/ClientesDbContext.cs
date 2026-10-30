using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Clientes.Application.Abstractions;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

/// <summary>
/// O CPF de clientes e dependentes é gravado cifrado (SEG-01). Como a cifra é aleatória, unicidade e busca usam a coluna
/// <c>cpf_hash</c> (<see cref="CpfHash"/>), preenchida aqui ao salvar.
/// </summary>
public sealed class ClientesDbContext(
    DbContextOptions<ClientesDbContext> options,
    ITenantContext tenantContext,
    ICriptografiaDados criptografia)
    : ModuleDbContext(options, tenantContext), IClientesUnitOfWork
{
    public const string SchemaName = "clientes";

    /// <summary>Propriedade de sombra com o HMAC do CPF.</summary>
    public const string CpfHash = nameof(CpfHash);

    public override string Schema => SchemaName;

    internal DbSet<Cliente> Clientes => Set<Cliente>();

    internal DbSet<Dependente> Dependentes => Set<Dependente>();

    /// <summary>Hash para buscar pelo CPF (já normalizado, só dígitos).</summary>
    internal string HashCpf(string cpf) => criptografia.Hash(cpf);

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entrada in ChangeTracker.Entries().Where(e => e.Entity is Cliente or Dependente && e.State is EntityState.Added or EntityState.Modified))
        {
            entrada.Property(CpfHash).CurrentValue = criptografia.Hash((string)entrada.Property(nameof(Cliente.Cpf)).CurrentValue!);
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClientesDbContext).Assembly);

        var cifra = new ValueConverter<string, string>(v => criptografia.Cifrar(v), v => criptografia.Decifrar(v));
        modelBuilder.Entity<Cliente>().Property(c => c.Cpf).HasConversion(cifra);
        modelBuilder.Entity<Dependente>().Property(d => d.Cpf).HasConversion(cifra);
    }
}
