using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Planos.Domain.Planos;

public static class PlanoErros
{
    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Plano.NaoEncontrado", "Plano não encontrado.");

    /// <summary>PLA-02: cobertura só com item existente e ativo no catálogo.</summary>
    public static Error ItensDeCoberturaInvalidos(IEnumerable<Guid> ids) =>
        new ValidationError(new Dictionary<string, string[]>
        {
            ["coberturas"] = [$"Item do catálogo não encontrado ou inativo: {string.Join(", ", ids)}."]
        });

    public static readonly Error NomeDuplicado =
        Error.Conflito("Plano.NomeDuplicado", "Já existe um plano com este nome.");
}
