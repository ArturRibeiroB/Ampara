using Funeraria.Modules.Contratos.Domain.Contratos;
using Microsoft.EntityFrameworkCore;

namespace Funeraria.Modules.Contratos.Infrastructure.Database;

internal sealed class ContratoRepository(ContratosDbContext context) : IContratoRepository
{
    private IQueryable<Contrato> Vigentes => context.Contratos.Where(c => c.Status != StatusContrato.Cancelado);

    public Task<Contrato?> ObterAsync(Guid idContrato, CancellationToken cancellationToken = default) =>
        context.Contratos.Include(c => c.PessoasCobertas).FirstOrDefaultAsync(c => c.Id == idContrato, cancellationToken);

    public Task<bool> ExisteVigenteAsync(Guid idCliente, Guid idPlano, CancellationToken cancellationToken = default) =>
        Vigentes.AnyAsync(c => c.IdCliente == idCliente && c.IdPlano == idPlano, cancellationToken);

    public async Task<IReadOnlyList<Guid>> DependentesCobertosAsync(
        IReadOnlyCollection<Guid> idsDependentes,
        CancellationToken cancellationToken = default) =>
        await Vigentes
            .SelectMany(c => c.PessoasCobertas)
            .Where(p => p.IdDependente != null && p.DataRemocao == null && idsDependentes.Contains(p.IdDependente.Value))
            .Select(p => p.IdDependente!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public void Adicionar(Contrato contrato) => context.Contratos.Add(contrato);
}
