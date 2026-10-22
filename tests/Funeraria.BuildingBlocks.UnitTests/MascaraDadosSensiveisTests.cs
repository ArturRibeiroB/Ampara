using Funeraria.BuildingBlocks.Infrastructure.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Funeraria.BuildingBlocks.UnitTests;

public sealed class MascaraDadosSensiveisTests
{
    [Fact]
    public void Senha_token_e_cpf_nao_aparecem_no_log()
    {
        LogEvent evento = Capturar(log => log.Information(
            "Login {Email} {Senha} {AccessToken} {Cpf} {Nome}",
            "joao@email.com", "Senha@123", "eyJhbGciOi", "52998224725", "João"));

        Assert.Equal("***", Texto(evento, "Senha"));
        Assert.Equal("***", Texto(evento, "AccessToken"));
        Assert.Equal("***725", Texto(evento, "Cpf"));
        Assert.Equal("João", Texto(evento, "Nome"));
        Assert.Equal("joao@email.com", Texto(evento, "Email"));
    }

    [Fact]
    public void Dados_sensiveis_dentro_de_objetos_tambem_sao_escondidos()
    {
        var comando = new { Nome = "João", Senha = "Senha@123", Endereco = new { Cidade = "Maringá", RefreshToken = "abc" } };

        LogEvent evento = Capturar(log => log.Information("Comando {@Comando}", comando));

        var estrutura = (StructureValue)evento.Properties["Comando"];
        Assert.Equal("***", Valor(estrutura, "Senha"));
        Assert.Equal("João", Valor(estrutura, "Nome"));
        var endereco = (StructureValue)estrutura.Properties.Single(p => p.Name == "Endereco").Value;
        Assert.Equal("***", Valor(endereco, "RefreshToken"));
        Assert.Equal("Maringá", Valor(endereco, "Cidade"));
    }

    private static LogEvent Capturar(Action<ILogger> escrever)
    {
        var sink = new Coletor();
        using Logger logger = new LoggerConfiguration().Enrich.With<MascaraDadosSensiveis>().WriteTo.Sink(sink).CreateLogger();
        escrever(logger);
        return Assert.Single(sink.Eventos);
    }

    private static string? Texto(LogEvent evento, string propriedade) =>
        ((ScalarValue)evento.Properties[propriedade]).Value?.ToString();

    private static string? Valor(StructureValue estrutura, string propriedade) =>
        ((ScalarValue)estrutura.Properties.Single(p => p.Name == propriedade).Value).Value?.ToString();

    private sealed class Coletor : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = [];

        public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
    }
}
