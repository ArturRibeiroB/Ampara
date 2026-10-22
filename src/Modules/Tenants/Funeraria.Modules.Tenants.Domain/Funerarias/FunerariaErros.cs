using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Tenants.Domain.Funerarias;

public static class FunerariaErros
{
    public static readonly Error NaoEncontrada =
        Error.NaoEncontrado("Funeraria.NaoEncontrada", "Funerária não encontrada.");

    public static readonly Error CnpjDuplicado =
        Error.Conflito("Funeraria.CnpjDuplicado", "CNPJ já cadastrado.");

    public static readonly Error SlugEmUso =
        Error.Conflito("Funeraria.SlugEmUso", "Este endereço já está em uso. Escolha outro.");

    public static Error TransicaoInvalida(StatusFuneraria atual, StatusFuneraria novo) =>
        Error.Conflito("Funeraria.TransicaoInvalida", $"Não é possível mudar o status de {Nome(atual)} para {Nome(novo)}.");

    private static string Nome(StatusFuneraria status) => status switch
    {
        StatusFuneraria.Pendente => "PENDENTE",
        StatusFuneraria.Ativa => "ATIVA",
        StatusFuneraria.Suspensa => "SUSPENSA",
        _ => status.ToString()
    };
}
