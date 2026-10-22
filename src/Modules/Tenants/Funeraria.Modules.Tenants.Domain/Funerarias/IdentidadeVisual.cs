using Funeraria.BuildingBlocks.Domain.ValueObjects;

namespace Funeraria.Modules.Tenants.Domain.Funerarias;

/// <summary>Como a funerária aparece para os clientes (TEN-03). Gravada em jsonb.</summary>
public sealed record IdentidadeVisual
{
    public string NomeExibicao { get; init; } = string.Empty;

    /// <summary>
    /// Endereço público do logo, servido pela API. Definido pelo upload (POST /gestao/configuracoes/logo);
    /// o valor enviado no PUT das configurações é ignorado.
    /// </summary>
    public string? LogoUrl { get; init; }

    public string CorPrimaria { get; init; } = "#1F3A5F";

    public string CorSecundaria { get; init; } = "#C9A227";

    public string? Email { get; init; }

    public string? Telefone { get; init; }

    public string? Whatsapp { get; init; }

    public Endereco Endereco { get; init; } = new();

    /// <summary>Texto livre, ex.: "Atendimento 24 horas".</summary>
    public string? HorarioAtendimento { get; init; }
}
