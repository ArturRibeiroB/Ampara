using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Funeraria.Api.Middlewares;

/// <summary>
/// Painel do Hangfire em /hangfire, só para o ADMIN (INF-11).
/// </summary>
/// <remarks>
/// O navegador não manda o JWT no cabeçalho ao abrir uma página. Por isso a área do ADMIN no frontend abre
/// <c>/hangfire?access_token=...</c>: a API guarda o token num cookie httpOnly restrito a /hangfire, tira o token da URL
/// e, a partir daí, lê o token desse cookie nas requisições do painel. O filtro exige o perfil ADMIN em todas.
/// </remarks>
internal static class PainelJobsExtensions
{
    private const string Caminho = "/hangfire";
    private const string Cookie = "funeraria_painel";

    public static IServiceCollection AddPainelJobs(this IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            Func<MessageReceivedContext, Task> original = options.Events.OnMessageReceived;
            options.Events.OnMessageReceived = async context =>
            {
                await original(context);
                if (context.Token is null && context.Request.Path.StartsWithSegments(Caminho))
                {
                    context.Token = context.Request.Cookies[Cookie];
                }
            };
        });

        return services;
    }

    public static WebApplication MapPainelJobs(this WebApplication app)
    {
        JobsOptions opcoes = app.Configuration.GetSection(JobsOptions.Section).Get<JobsOptions>() ?? new JobsOptions();
        if (!opcoes.PainelHabilitado)
        {
            return app;
        }

        // Troca o token da URL pelo cookie e redireciona, para o token não ficar no histórico do navegador.
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(Caminho) && context.Request.Query["access_token"] is { Count: > 0 } token)
            {
                context.Response.Cookies.Append(Cookie, token.ToString(), new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Path = Caminho,
                    MaxAge = TimeSpan.FromMinutes(15)
                });
                context.Response.Redirect(Caminho);
                return;
            }

            await next(context);
        });

        app.MapHangfireDashboard(Caminho, new DashboardOptions
        {
            DashboardTitle = "Funerária Online · Jobs",
            Authorization = [new SomenteAdmin()],
            AppPath = null
        })
        .AllowAnonymous(); // a autorização do painel é o filtro abaixo, que exige o ADMIN

        return app;
    }

    private sealed class SomenteAdmin : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context) =>
            context.GetHttpContext().User.IsInRole(Perfis.Admin);
    }
}
