using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;

namespace Funeraria.BuildingBlocks.Infrastructure.Http;

/// <summary>
/// Chamadas a APIs externas (INF-12): pagamento, certidão, distância. Todo cliente tipado passa por retry com backoff
/// exponencial, circuit breaker e timeout, com as tentativas e as aberturas de circuito no log.
/// </summary>
public static class ClientesExternos
{
    /// <summary>Registra um HttpClient tipado com a política padrão de resiliência (seção <c>Resiliencia</c>).</summary>
    public static IHttpClientBuilder AddClienteExterno<TCliente, TImplementacao>(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<HttpClient> configurar)
        where TCliente : class
        where TImplementacao : class, TCliente
    {
        ResilienciaOptions opcoes = configuration.GetSection(ResilienciaOptions.Section).Get<ResilienciaOptions>() ?? new ResilienciaOptions();
        string nome = typeof(TImplementacao).Name;

        IHttpClientBuilder builder = services.AddHttpClient<TCliente, TImplementacao>(configurar);
        builder.AddResilienceHandler("externo", (pipeline, contexto) =>
        {
            ILogger logger = contexto.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Funeraria.ClientesExternos");

            pipeline
                .AddTimeout(opcoes.TimeoutTotal)
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = opcoes.Tentativas,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    Delay = opcoes.EsperaInicial,
                    OnRetry = args =>
                    {
                        logger.LogWarning(
                            "Chamada a {Cliente} falhou ({Motivo}); tentativa {Tentativa} de {Maximo} em {Espera} ms.",
                            nome, Motivo(args.Outcome), args.AttemptNumber + 1, opcoes.Tentativas, args.RetryDelay.TotalMilliseconds);
                        return ValueTask.CompletedTask;
                    }
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = opcoes.ProporcaoDeFalhas,
                    MinimumThroughput = opcoes.MinimoDeChamadas,
                    SamplingDuration = opcoes.JanelaDeAmostragem,
                    BreakDuration = opcoes.TempoAberto,
                    OnOpened = args =>
                    {
                        logger.LogError(
                            "Circuito de {Cliente} aberto por {Tempo} s depois de falhas seguidas ({Motivo}). As chamadas falham na hora até ele fechar.",
                            nome, args.BreakDuration.TotalSeconds, Motivo(args.Outcome));
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogInformation("Circuito de {Cliente} fechado: o serviço voltou a responder.", nome);
                        return ValueTask.CompletedTask;
                    }
                })
                .AddTimeout(opcoes.TimeoutPorTentativa);
        });

        return builder;
    }

    private static string Motivo(Outcome<HttpResponseMessage> resultado) =>
        resultado.Exception?.GetType().Name ?? $"HTTP {(int)(resultado.Result?.StatusCode ?? HttpStatusCode.InternalServerError)}";
}

/// <summary>Seção <c>Resiliencia</c>. Os padrões servem para APIs de terceiros com resposta em poucos segundos.</summary>
public sealed class ResilienciaOptions
{
    public const string Section = "Resiliencia";

    public int Tentativas { get; set; } = 3;

    public TimeSpan EsperaInicial { get; set; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan TimeoutPorTentativa { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan TimeoutTotal { get; set; } = TimeSpan.FromSeconds(40);

    /// <summary>Proporção de falhas na janela que abre o circuito.</summary>
    public double ProporcaoDeFalhas { get; set; } = 0.5;

    public int MinimoDeChamadas { get; set; } = 10;

    public TimeSpan JanelaDeAmostragem { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan TempoAberto { get; set; } = TimeSpan.FromSeconds(30);
}
