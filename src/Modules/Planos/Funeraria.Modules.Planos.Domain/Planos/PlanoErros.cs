using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Planos.Domain.Planos;

public static class PlanoErros
{
    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Plano.NaoEncontrado", "Plano não encontrado.");

    public static readonly Error NomeDuplicado =
        Error.Conflito("Plano.NomeDuplicado", "Já existe um plano com este nome.");
}
