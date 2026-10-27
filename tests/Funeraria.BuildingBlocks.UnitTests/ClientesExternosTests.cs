using System.Collections.Concurrent;
using System.Net;
using Funeraria.BuildingBlocks.Infrastructure.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace Funeraria.BuildingBlocks.UnitTests;

/// <summary>INF-12: retry, circuit breaker e log das falhas de uma API externa simulada.</summary>
public sealed class ClientesExternosTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Falha_passageira_e_repetida_ate_dar_certo_e_vai_para_o_log()
    {
        var api = new ApiFalsa(falhasAntesDeResponder: 2);
        var logs = new LogsCapturados();
        ApiExterna cliente = Montar(api, logs);

        string resposta = await cliente.ConsultarAsync(Ct);

        Assert.Equal("ok", resposta);
        Assert.Equal(3, api.Chamadas);
        Assert.Equal(2, logs.Mensagens.Count(m => m.Contains("tentativa", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Api_fora_do_ar_abre_o_circuito_e_as_chamadas_seguintes_falham_na_hora()
    {
        var api = new ApiFalsa(falhasAntesDeResponder: int.MaxValue);
        var logs = new LogsCapturados();
        ApiExterna cliente = Montar(api, logs);

        await Assert.ThrowsAnyAsync<Exception>(() => cliente.ConsultarAsync(Ct));
        await Assert.ThrowsAnyAsync<Exception>(() => cliente.ConsultarAsync(Ct));
        int chamadasAntes = api.Chamadas;

        await Assert.ThrowsAsync<BrokenCircuitException>(() => cliente.ConsultarAsync(Ct));
        Assert.Equal(chamadasAntes, api.Chamadas);
        Assert.Contains(logs.Mensagens, m => m.Contains("Circuito de ApiExterna aberto", StringComparison.Ordinal));
    }

    private static ApiExterna Montar(ApiFalsa api, LogsCapturados logs)
    {
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resiliencia:Tentativas"] = "2",
                ["Resiliencia:EsperaInicial"] = "00:00:00.010",
                ["Resiliencia:MinimoDeChamadas"] = "3",
                ["Resiliencia:ProporcaoDeFalhas"] = "0.5",
                ["Resiliencia:TempoAberto"] = "00:01:00"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(l => l.AddProvider(logs));
        services.AddClienteExterno<ApiExterna, ApiExterna>(configuracao, c => c.BaseAddress = new Uri("http://api.externa.teste"))
            .ConfigurePrimaryHttpMessageHandler(() => api);
        return services.BuildServiceProvider().GetRequiredService<ApiExterna>();
    }

    public sealed class ApiExterna(HttpClient http)
    {
        public async Task<string> ConsultarAsync(CancellationToken cancellationToken)
        {
            HttpResponseMessage resposta = await http.GetAsync("/status", cancellationToken);
            resposta.EnsureSuccessStatusCode();
            return await resposta.Content.ReadAsStringAsync(cancellationToken);
        }
    }

    private sealed class ApiFalsa(int falhasAntesDeResponder) : HttpMessageHandler
    {
        private int _chamadas;

        public int Chamadas => _chamadas;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(Interlocked.Increment(ref _chamadas) <= falhasAntesDeResponder
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
    }

    private sealed class LogsCapturados : ILoggerProvider
    {
        public ConcurrentQueue<string> Mensagens { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(Mensagens);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<string> mensagens) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                mensagens.Enqueue(formatter(state, exception));
        }
    }
}
