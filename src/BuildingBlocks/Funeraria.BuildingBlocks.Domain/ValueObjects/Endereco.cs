namespace Funeraria.BuildingBlocks.Domain.ValueObjects;

/// <summary>
/// Endereço brasileiro. Value object do shared kernel: usado pela funerária, pelo cliente e, depois, pelo translado.
/// Todos os campos são opcionais aqui; cada caso de uso decide quais exige.
/// </summary>
public sealed record Endereco
{
    public string? Logradouro { get; init; }

    public string? Numero { get; init; }

    public string? Complemento { get; init; }

    public string? Bairro { get; init; }

    public string? Cidade { get; init; }

    public string? Uf { get; init; }

    public string? Cep { get; init; }
}
