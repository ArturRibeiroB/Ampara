using System.Text.Json;
using System.Text.Json.Serialization;

namespace Funeraria.BuildingBlocks.Application.Serialization;

/// <summary>
/// Opções de serialização das colunas jsonb: chaves em snake_case, iguais às dos cards (PLA-03, TEN-03).
/// Usadas tanto no mapeamento do EF Core quanto na leitura com Dapper.
/// </summary>
public static class JsonbOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) }
    };
}
