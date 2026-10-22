using Serilog.Core;
using Serilog.Events;

namespace Funeraria.BuildingBlocks.Infrastructure.Logging;

/// <summary>
/// Esconde dados sensíveis nos logs (INF-08, SEG-01): propriedades com nome de senha, CPF ou token são trocadas
/// por "***", inclusive dentro de objetos e listas. O CPF fica só com os 3 últimos dígitos, para ainda ajudar no suporte.
/// </summary>
public sealed class MascaraDadosSensiveis : ILogEventEnricher
{
    private const string Oculto = "***";

    private static readonly string[] Ocultar =
    [
        "senha", "password", "token", "secret", "chave", "authorization", "cookie"
    ];

    private static readonly string[] Cpf = ["cpf"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach ((string nome, LogEventPropertyValue valor) in logEvent.Properties.ToList())
        {
            LogEventPropertyValue mascarado = Mascarar(nome, valor);
            if (!ReferenceEquals(mascarado, valor))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(nome, mascarado));
            }
        }
    }

    private static LogEventPropertyValue Mascarar(string nome, LogEventPropertyValue valor)
    {
        string normalizado = nome.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

        if (Ocultar.Any(normalizado.Contains))
        {
            return new ScalarValue(Oculto);
        }

        if (Cpf.Any(normalizado.Contains))
        {
            return valor is ScalarValue { Value: string cpf } && cpf.Length >= 3
                ? new ScalarValue($"***{cpf[^3..]}")
                : new ScalarValue(Oculto);
        }

        return valor switch
        {
            StructureValue estrutura => MascararEstrutura(estrutura),
            DictionaryValue dicionario => MascararDicionario(dicionario),
            SequenceValue sequencia => MascararSequencia(sequencia),
            _ => valor
        };
    }

    private static LogEventPropertyValue MascararEstrutura(StructureValue estrutura)
    {
        var propriedades = estrutura.Properties.Select(p => new LogEventProperty(p.Name, Mascarar(p.Name, p.Value))).ToList();
        return propriedades.Zip(estrutura.Properties).All(par => ReferenceEquals(par.First.Value, par.Second.Value))
            ? estrutura
            : new StructureValue(propriedades, estrutura.TypeTag);
    }

    private static LogEventPropertyValue MascararDicionario(DictionaryValue dicionario)
    {
        var elementos = dicionario.Elements
            .Select(e => new KeyValuePair<ScalarValue, LogEventPropertyValue>(e.Key, Mascarar(e.Key.Value?.ToString() ?? string.Empty, e.Value)))
            .ToList();

        return elementos.Zip(dicionario.Elements).All(par => ReferenceEquals(par.First.Value, par.Second.Value))
            ? dicionario
            : new DictionaryValue(elementos);
    }

    private static LogEventPropertyValue MascararSequencia(SequenceValue sequencia)
    {
        var elementos = sequencia.Elements.Select(e => Mascarar(string.Empty, e)).ToList();
        return elementos.Zip(sequencia.Elements).All(par => ReferenceEquals(par.First, par.Second))
            ? sequencia
            : new SequenceValue(elementos);
    }
}
