using System.Threading.RateLimiting;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Funeraria.Api.Middlewares;

/// <summary>Limite de requisições por IP nos endpoints públicos sensíveis (TEN-02, SEG-02). Excesso → 429.</summary>
internal static class RateLimitingExtensions
{
    public static IServiceCollection AddLimiteDeRequisicoes(this IServiceCollection services, IConfiguration configuration)
    {
        var onboarding = configuration.GetSection("LimiteRequisicoes:Onboarding").Get<LimiteOptions>() ?? new LimiteOptions();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PoliticasLimite.Onboarding, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = onboarding.Limite,
                    Window = onboarding.Janela,
                    QueueLimit = 0
                }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                HttpContext http = context.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Muitas requisições",
                        Detail = "Muitas tentativas em pouco tempo. Aguarde e tente novamente.",
                        Extensions = { ["code"] = "Requisicao.LimiteExcedido" }
                    }
                });
            };
        });

        return services;
    }

    private sealed class LimiteOptions
    {
        public int Limite { get; set; } = 5;

        public TimeSpan Janela { get; set; } = TimeSpan.FromHours(1);
    }
}
