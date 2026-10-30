using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Funeraria.BuildingBlocks.Infrastructure.Endpoints;

public static class EndpointExtensions
{
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        ServiceDescriptor[] descriptors = assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false } && t.IsAssignableTo(typeof(IEndpoint)))
            .Select(t => ServiceDescriptor.Transient(typeof(IEndpoint), t))
            .ToArray();

        services.TryAddEnumerable(descriptors);
        return services;
    }

    public static IEndpointRouteBuilder MapEndpoints(this IEndpointRouteBuilder app)
    {
        foreach (IEndpoint endpoint in app.ServiceProvider.GetRequiredService<IEnumerable<IEndpoint>>())
        {
            endpoint.MapEndpoint(app);
        }

        return app;
    }

    /// <summary>
    /// Marca o endpoint como obsoleto (<c>deprecated</c> no OpenAPI) antes de removê-lo numa versão maior (INF-21).
    /// Registre também no CHANGELOG, em "Obsoleto", com o que usar no lugar.
    /// </summary>
    public static TBuilder MarcarObsoleto<TBuilder>(this TBuilder builder, string useNoLugar)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(new ObsoleteAttribute($"Obsoleto. Use {useNoLugar}."));
}
