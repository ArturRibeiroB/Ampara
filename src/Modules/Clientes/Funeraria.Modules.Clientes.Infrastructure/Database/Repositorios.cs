using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

internal sealed class ClienteRepository(ClientesDbContext context) : IClienteRepository
{
    public Task<Cliente?> ObterPorIdAsync(Guid idCliente, CancellationToken cancellationToken = default) =>
        context.Clientes.FirstOrDefaultAsync(c => c.Id == idCliente, cancellationToken);

    public Task<bool> ExisteCpfAsync(string cpf, CancellationToken cancellationToken = default) =>
        context.Clientes.AnyAsync(c => c.Cpf == cpf, cancellationToken);

    public void Adicionar(Cliente cliente) => context.Clientes.Add(cliente);
}

/// <summary>
/// O soft delete fica explícito nas consultas, e não num filtro global: o filtro global do DbContext já é o de tenant,
/// e um segundo <c>HasQueryFilter</c> substituiria o primeiro.
/// </summary>
internal sealed class DependenteRepository(ClientesDbContext context) : IDependenteRepository
{
    private IQueryable<Dependente> Ativos => context.Dependentes.Where(d => d.RemovidoEm == null);

    public Task<Dependente?> ObterDoTitularAsync(Guid idDependente, Guid idCliente, CancellationToken cancellationToken = default) =>
        Ativos.FirstOrDefaultAsync(d => d.Id == idDependente && d.IdCliente == idCliente, cancellationToken);

    public async Task<IReadOnlyList<Dependente>> ListarDoTitularAsync(Guid idCliente, CancellationToken cancellationToken = default) =>
        await Ativos.AsNoTracking().Where(d => d.IdCliente == idCliente).OrderBy(d => d.Nome).ToListAsync(cancellationToken);

    public Task<bool> ExisteCpfDoTitularAsync(Guid idCliente, string cpf, CancellationToken cancellationToken = default) =>
        Ativos.AnyAsync(d => d.IdCliente == idCliente && d.Cpf == cpf, cancellationToken);

    public void Adicionar(Dependente dependente) => context.Dependentes.Add(dependente);
}
