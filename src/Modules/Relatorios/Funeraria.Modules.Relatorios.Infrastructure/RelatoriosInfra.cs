using Funeraria.Modules.Relatorios.Application;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Funeraria.Modules.Relatorios.Infrastructure;

internal sealed class CacheDeRelatorios(IMemoryCache cache) : ICacheDeRelatorios
{
    public async Task<T> ObterAsync<T>(string chave, TimeSpan validade, Func<Task<T>> gerar) =>
        (await cache.GetOrCreateAsync(chave, entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = validade;
            return gerar();
        }))!;
}

/// <summary>A taxa é configurada junto com o Stripe, que é quem a retém (DEC-09).</summary>
internal sealed class ParametrosPlataforma(IConfiguration configuration) : IParametrosPlataforma
{
    public decimal TaxaPlataformaPercentual => configuration.GetValue("Stripe:TaxaPlataformaPercentual", 2m);
}
