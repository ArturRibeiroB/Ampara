using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Cache;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.UnitTests;

/// <summary>INF-19: cada funerária tem o próprio cache, e invalidar uma não mexe na outra. Sem Redis, só memória.</summary>
public sealed class CacheDaFunerariaTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Mesma_area_e_chave_em_funerarias_diferentes_nao_se_misturam()
    {
        await using ServiceProvider services = Servicos();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();

        string deA = await LerAsync(services, a, "planos de A");
        string deB = await LerAsync(services, b, "planos de B");

        Assert.Equal("planos de A", deA);
        Assert.Equal("planos de B", deB);
        Assert.Equal("planos de A", await LerAsync(services, a, "não deveria carregar de novo"));
    }

    [Fact]
    public async Task Invalidar_uma_funeraria_recarrega_so_ela()
    {
        await using ServiceProvider services = Servicos();
        Guid a = Guid.NewGuid();
        Guid b = Guid.NewGuid();
        await LerAsync(services, a, "versão 1 de A");
        await LerAsync(services, b, "versão 1 de B");

        await using (AsyncServiceScope scope = services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICacheDaFuneraria>().InvalidarAsync(a, AreasCache.Planos, Ct);
        }

        Assert.Equal("versão 2 de A", await LerAsync(services, a, "versão 2 de A"));
        Assert.Equal("versão 1 de B", await LerAsync(services, b, "versão 2 de B"));
    }

    [Fact]
    public void Chave_e_grupo_levam_o_id_da_funeraria()
    {
        Guid tenant = Guid.NewGuid();

        Assert.Equal($"tenant:{tenant:N}:planos:ativos", AreasCache.Chave(tenant, AreasCache.Planos, "ativos"));
        Assert.Equal($"tenant:{tenant:N}:planos", AreasCache.Grupo(tenant, AreasCache.Planos));
    }

    private static async Task<string> LerAsync(ServiceProvider services, Guid tenant, string valorSeCarregar)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Set(tenant);
        return await scope.ServiceProvider.GetRequiredService<ICacheDaFuneraria>()
            .ObterAsync(AreasCache.Planos, "ativos", _ => ValueTask.FromResult(valorSeCarregar), Ct);
    }

    private static ServiceProvider Servicos()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddCacheDaFuneraria(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }
}
