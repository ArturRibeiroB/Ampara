using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Contratos.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Contratos.Infrastructure;

public static class ContratosModule
{
    public static IServiceCollection AddContratosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddScoped<IContratosApi, ContratosApiSemContratos>();

        return services;
    }

    /// <summary>
    /// Provisório até existirem contratos (Criar Contrato, CON-01/CON-02): ninguém tem contrato ativo.
    /// Os outros módulos já dependem da interface, então só esta classe muda.
    /// </summary>
    private sealed class ContratosApiSemContratos : IContratosApi
    {
        public Task<bool> DependentePossuiContratoAtivoAsync(Guid idDependente, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
