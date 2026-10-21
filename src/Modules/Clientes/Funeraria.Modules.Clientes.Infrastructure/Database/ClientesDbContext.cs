using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Clientes.Application.Abstractions;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

public sealed class ClientesDbContext(DbContextOptions<ClientesDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), IClientesUnitOfWork
{
    public const string SchemaName = "clientes";

    public override string Schema => SchemaName;

    internal DbSet<Cliente> Clientes => Set<Cliente>();

    internal DbSet<Dependente> Dependentes => Set<Dependente>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClientesDbContext).Assembly);
}
