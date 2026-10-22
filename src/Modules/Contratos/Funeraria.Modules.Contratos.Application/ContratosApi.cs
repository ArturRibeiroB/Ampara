using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.Application;

public sealed class ContratosApi(IContratoRepository contratos) : IContratosApi
{
    public async Task<bool> DependentePossuiContratoAtivoAsync(Guid idDependente, CancellationToken cancellationToken = default) =>
        (await contratos.DependentesCobertosAsync([idDependente], cancellationToken)).Count > 0;
}
