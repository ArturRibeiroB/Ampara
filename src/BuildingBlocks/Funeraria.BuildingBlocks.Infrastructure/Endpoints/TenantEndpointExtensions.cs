using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Domain.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.BuildingBlocks.Infrastructure.Endpoints;

public static class TenantEndpointExtensions
{
    public static readonly Error TenantNaoIdentificado = Error.Validacao(
        "Tenant.NaoIdentificado",
        "Funerária não identificada. Acesse pelo endereço da funerária.");

    /// <summary>
    /// Exige que a requisição tenha uma funerária resolvida (subdomínio, cabeçalho X-Tenant ou JWT).
    /// Use em endpoints públicos que mostram dados de uma funerária, como GET /planos.
    /// </summary>
    public static TBuilder RequireTenant<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            ITenantContext tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            return tenant.HasTenant
                ? await next(context)
                : ApiResults.Problem(Result.Failure(TenantNaoIdentificado));
        });
}
