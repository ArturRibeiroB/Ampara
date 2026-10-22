using Funeraria.Modules.Clientes.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

internal sealed class ClientesApi(ClientesDbContext context) : IClientesApi
{
    public Task<TitularDto?> ObterTitularAsync(Guid idCliente, CancellationToken cancellationToken = default) =>
        context.Clientes
            .AsNoTracking()
            .Where(c => c.Id == idCliente)
            .Select(c => new TitularDto(c.Id, c.Nome, c.Cpf))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<DependenteDto>> ObterDependentesAsync(
        IReadOnlyCollection<Guid> idsDependente,
        CancellationToken cancellationToken = default) =>
        idsDependente.Count == 0
            ? []
            : await context.Dependentes
                .AsNoTracking()
                .Where(d => idsDependente.Contains(d.Id) && d.RemovidoEm == null)
                .Select(d => new DependenteDto(d.Id, d.IdCliente, d.Nome, d.Cpf))
                .ToListAsync(cancellationToken);
}
