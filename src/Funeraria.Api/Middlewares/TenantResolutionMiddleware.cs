using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Tenants.Contracts;
using Microsoft.Extensions.Options;

namespace Funeraria.Api.Middlewares;

/// <summary>
/// Resolve a funerária da requisição (TEN-01, DEC-02):
/// <list type="number">
/// <item>Lê o slug do subdomínio (<c>demo.funeraria.app</c>) ou do cabeçalho <c>X-Tenant</c>, enviado pelo frontend.</item>
/// <item>Busca o id_tenant do slug no módulo Tenants.</item>
/// <item>Se o JWT tiver <c>id_tenant</c>, confere se é o mesmo da funerária do slug.</item>
/// <item>Guarda o tenant no <see cref="ITenantContext"/> da requisição.</item>
/// </list>
/// Roda depois da autenticação e antes da autorização.
/// </summary>
internal sealed class TenantResolutionMiddleware(RequestDelegate next, IOptions<TenancyOptions> options)
{
    public const string TenantHeader = "X-Tenant";

    private static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Tenant.NaoEncontrado", "Funerária não encontrada.");

    private static readonly Error Suspensa =
        Error.Proibido("Tenant.Suspensa", "Esta funerária está suspensa.");

    private static readonly Error AcessoNegado =
        Error.Proibido("Tenant.AcessoNegado", "Você não possui acesso a esta funerária.");

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, ITenantsApi tenants, ICurrentUser currentUser)
    {
        Guid? tenantDoToken = currentUser.IdTenant;
        Guid? tenantDoSlug = null;

        string? slug = ObterSlug(context);
        if (slug is not null)
        {
            FunerariaResumoDto? funeraria = await tenants.ObterPorSlugAsync(slug, context.RequestAborted);
            if (funeraria is null)
            {
                await ApiResults.Problem(Result.Failure(NaoEncontrado)).ExecuteAsync(context);
                return;
            }

            if (funeraria.Suspensa)
            {
                await ApiResults.Problem(Result.Failure(Suspensa)).ExecuteAsync(context);
                return;
            }

            tenantDoSlug = funeraria.IdTenant;
        }

        if (tenantDoToken is not null && tenantDoSlug is not null && tenantDoToken != tenantDoSlug)
        {
            await ApiResults.Problem(Result.Failure(AcessoNegado)).ExecuteAsync(context);
            return;
        }

        tenantContext.Set(tenantDoToken ?? tenantDoSlug);

        await next(context);
    }

    private string? ObterSlug(HttpContext context)
    {
        string? header = context.Request.Headers[TenantHeader];
        if (!string.IsNullOrWhiteSpace(header))
        {
            return header.Trim().ToLowerInvariant();
        }

        string host = context.Request.Host.Host.ToLowerInvariant();
        string sufixo = "." + options.Value.DominioBase.ToLowerInvariant();
        if (!host.EndsWith(sufixo, StringComparison.Ordinal))
        {
            return null;
        }

        string subdominio = host[..^sufixo.Length];
        if (subdominio.Contains('.', StringComparison.Ordinal) || options.Value.SubdominiosReservados.Contains(subdominio))
        {
            return null;
        }

        return subdominio;
    }
}

internal sealed class TenancyOptions
{
    public const string Section = "Tenancy";

    /// <summary>Domínio da plataforma. Em desenvolvimento, "localhost" (ex.: demo.localhost).</summary>
    public string DominioBase { get; set; } = "localhost";

    /// <summary>Subdomínios da plataforma que não são funerárias (TEN-02).</summary>
    public HashSet<string> SubdominiosReservados { get; set; } = ["www", "api", "admin", "app"];
}
