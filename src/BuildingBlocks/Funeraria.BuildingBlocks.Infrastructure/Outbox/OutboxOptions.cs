namespace Funeraria.BuildingBlocks.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string Section = "Outbox";

    /// <summary>Intervalo entre as leituras do outbox de cada módulo.</summary>
    public TimeSpan Intervalo { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Quantidade de mensagens lidas por vez.</summary>
    public int TamanhoLote { get; set; } = 20;

    /// <summary>Depois desse número de falhas, a mensagem é marcada como processada com o erro gravado.</summary>
    public int MaximoTentativas { get; set; } = 5;
}
