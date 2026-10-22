using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Features;
using IPNetwork = System.Net.IPNetwork;

namespace Funeraria.Api.Middlewares;

/// <summary>
/// Hardening da API (SEG-02): CORS só para a plataforma e os subdomínios das funerárias, HTTPS/HSTS,
/// cabeçalhos de segurança, limite de tamanho das requisições e IP real atrás do proxy.
/// </summary>
internal static class SegurancaExtensions
{
    public const string PoliticaCors = "Plataforma";

    public static IServiceCollection AddSeguranca(this IServiceCollection services, IConfiguration configuration)
    {
        SegurancaOptions opcoes = configuration.GetSection(SegurancaOptions.Section).Get<SegurancaOptions>() ?? new SegurancaOptions();
        string dominioBase = configuration["Tenancy:DominioBase"] ?? "localhost";

        services.AddCors(options => options.AddPolicy(PoliticaCors, policy => policy
            .SetIsOriginAllowed(origem => OrigemPermitida(origem, dominioBase, opcoes))
            .WithHeaders("Authorization", "Content-Type", "X-Tenant", CorrelationIdMiddleware.Header)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithExposedHeaders(CorrelationIdMiddleware.Header, "Location", "Retry-After")
            // O refresh token vai em cookie: o frontend precisa enviar credenciais.
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromHours(1))));

        services.AddHsts(options =>
        {
            options.MaxAge = TimeSpan.FromDays(365);
            options.IncludeSubDomains = true;
        });

        // IP e esquema reais vindos do proxy/balanceador. Sem isso o limite por IP contaria o IP do proxy.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (string rede in opcoes.RedesDeProxyConfiaveis)
            {
                options.KnownIPNetworks.Add(IPNetwork.Parse(rede));
            }
        });

        // Teto para qualquer requisição; cada upload valida o próprio limite, menor (INF-18).
        services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
            options.Limits.MaxRequestBodySize = opcoes.TamanhoMaximoRequisicaoBytes);
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = opcoes.TamanhoMaximoRequisicaoBytes);

        return services;
    }

    /// <summary>Ordem: logo no início do pipeline, antes de autenticação e rotas.</summary>
    public static WebApplication UseSeguranca(this WebApplication app)
    {
        SegurancaOptions opcoes = app.Configuration.GetSection(SegurancaOptions.Section).Get<SegurancaOptions>() ?? new SegurancaOptions();

        app.UseForwardedHeaders();

        if (opcoes.ForcarHttps)
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.Use(async (context, next) =>
        {
            IHeaderDictionary headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";

            // A API só devolve JSON. As páginas de documentação carregam scripts e ficam com a política padrão.
            if (!EhDocumentacao(context.Request.Path))
            {
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            }

            await next(context);
        });

        app.UseCors(PoliticaCors);
        return app;
    }

    private static bool EhDocumentacao(PathString path) =>
        path.StartsWithSegments("/swagger") || path.StartsWithSegments("/scalar") || path.StartsWithSegments("/openapi");

    /// <summary>Aceita o domínio da plataforma, os subdomínios das funerárias e as origens extras configuradas.</summary>
    internal static bool OrigemPermitida(string origem, string dominioBase, SegurancaOptions opcoes)
    {
        if (opcoes.OrigensExtras.Contains(origem, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(origem, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        bool esquemaOk = uri.Scheme == Uri.UriSchemeHttps || (opcoes.PermitirHttp && uri.Scheme == Uri.UriSchemeHttp);
        string host = uri.Host.ToLowerInvariant();
        string dominio = dominioBase.ToLowerInvariant();

        return esquemaOk && (host == dominio || host.EndsWith("." + dominio, StringComparison.Ordinal));
    }
}

internal sealed class SegurancaOptions
{
    public const string Section = "Seguranca";

    /// <summary>HSTS e redirecionamento para HTTPS. Desligado em desenvolvimento e no container local.</summary>
    public bool ForcarHttps { get; set; } = true;

    /// <summary>Aceita origens http:// no CORS. Só para desenvolvimento.</summary>
    public bool PermitirHttp { get; set; }

    /// <summary>Origens fora do domínio da plataforma (ex.: http://localhost:5173 do Vite).</summary>
    public string[] OrigensExtras { get; set; } = [];

    /// <summary>Redes do proxy/balanceador, em CIDR (ex.: 10.0.0.0/8). Só delas o X-Forwarded-For é aceito.</summary>
    public string[] RedesDeProxyConfiaveis { get; set; } = [];

    public long TamanhoMaximoRequisicaoBytes { get; set; } = 10 * 1024 * 1024;
}
