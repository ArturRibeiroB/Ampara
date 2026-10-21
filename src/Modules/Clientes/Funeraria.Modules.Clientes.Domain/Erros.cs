using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Clientes.Domain;

/// <summary>Erros do card Criar Cliente e do CLI-01.</summary>
public static class ClienteErros
{
    public static readonly Error CpfDuplicado =
        Error.Conflito("Cliente.CpfDuplicado", "CPF já cadastrado.");

    /// <summary>O e-mail já tem acesso a esta funerária (como cliente ou equipe).</summary>
    public static readonly Error EmailDuplicado =
        Error.Conflito("Cliente.EmailDuplicado", "E-mail já cadastrado.");

    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Cliente.NaoEncontrado", "Cliente não encontrado.");

    public const string MensagemSenha = "Senha não informada ou fora dos critérios definidos.";
}

/// <summary>Erros do card Criar Dependente e do CLI-02.</summary>
public static class DependenteErros
{
    public static readonly Error NaoEncontrado =
        Error.NaoEncontrado("Dependente.NaoEncontrado", "Dependente não encontrado.");

    public static readonly Error CpfDuplicado =
        Error.Conflito("Dependente.CpfDuplicado", "Este CPF já está cadastrado entre os dependentes.");

    public static readonly Error CpfDoTitular =
        Error.Conflito("Dependente.CpfDoTitular", "O CPF informado é o do próprio titular.");

    public static readonly Error VinculadoAContratoAtivo =
        Error.Conflito("Dependente.ContratoAtivo", "Dependente vinculado a contrato ativo não pode ser removido.");
}
