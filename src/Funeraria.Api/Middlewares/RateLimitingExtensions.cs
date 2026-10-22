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
        services.AddRateLimiter(options =>
        {
            PorIp(options, configuration, PoliticasLimite.Onboarding, new LimiteOptions { Limite = 5 });
            PorIp(options, configuration, PoliticasLimite.CadastroCliente, new LimiteOptions { Limite = 10 });
            PorIp(options, configuration, PoliticasLimite.Autenticacao, new LimiteOptions { Limite = 20, Janela = TimeSpan.FromMinutes(1) });

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

    /// <summary>Janela fixa por IP, configurável em <c>LimiteRequisicoes:{politica}</c>.</summary>
    private static void PorIp(RateLimiterOptions options, IConfiguration configuration, string politica, LimiteOptions padrao)
    {
        LimiteOptions limite = configuration.GetSection($"LimiteRequisicoes:{politica}").Get<LimiteOptions>() ?? padrao;

        options.AddPolicy(politica, http => RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limite.Limite,
                Window = limite.Janela,
                QueueLimit = 0
            }));
    }

    private sealed class LimiteOptions
    {
        public int Limite { get; set; } = 5;

        public TimeSpan Janela { get; set; } = TimeSpan.FromHours(1);
    }
}
