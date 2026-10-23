using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Identidade.Domain.Equipe;

/// <summary>
/// Dados de quem trabalha na funerária, funcionário ou consultor (AUT-07, DEC-11). A conta e o perfil ficam no
/// vínculo; o status (ativo ou não) também, para haver uma só fonte.
/// </summary>
/// <remarks>Como o vínculo, não usa o filtro global de tenant: o repositório filtra a funerária explicitamente.</remarks>
public sealed class Funcionario : AggregateRoot<Guid>, IAuditableEntity
{
    private Funcionario(Guid id)
        : base(id)
    {
    }

    private Funcionario()
    {
    }

    public Guid IdTenant { get; private set; }

    public Guid IdUsuario { get; private set; }

    public string Nome { get; private set; } = null!;

    /// <summary>Só dígitos. Único dentro da funerária.</summary>
    public string Cpf { get; private set; } = null!;

    public string? Cargo { get; private set; }

    public string? Telefone { get; private set; }

    public static Funcionario Criar(Guid idTenant, Guid idUsuario, string nome, string cpf, string? cargo, string? telefone) =>
        new(Guid.CreateVersion7())
        {
            IdTenant = idTenant,
            IdUsuario = idUsuario,
            Nome = nome.Trim(),
            Cpf = cpf,
            Cargo = string.IsNullOrWhiteSpace(cargo) ? null : cargo.Trim(),
            Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim()
        };
}

public interface IFuncionarioRepository
{
    Task<bool> ExisteCpfAsync(Guid idTenant, string cpf, CancellationToken cancellationToken = default);

    void Adicionar(Funcionario funcionario);
}

/// <summary>Erros do AUT-07 e do AUT-08, com as mensagens dos cards.</summary>
public static class EquipeErros
{
    public const string SomenteGestorCadastra = "Acesso negado. Apenas o gestor da funerária pode cadastrar funcionários.";

    public static readonly Error JaEhDaEquipe =
        Error.Conflito("Equipe.JaEhDaEquipe", "Este usuário já é funcionário desta funerária.");

    public static readonly Error EmailDeCliente =
        Error.Conflito("Equipe.EmailDeCliente", "Este e-mail é de um cliente desta funerária. Use outro e-mail para a equipe.");

    public static readonly Error CpfDuplicado =
        Error.Conflito("Equipe.CpfDuplicado", "CPF já cadastrado nesta funerária.");

    public static readonly Error UsuarioNaoEncontrado =
        Error.NaoEncontrado("Equipe.UsuarioNaoEncontrado", "Usuário não encontrado nesta funerária.");

    public static readonly Error ProprioAcesso =
        Error.Conflito("Equipe.ProprioAcesso", "Você não pode desativar o seu próprio acesso.");

    public static readonly Error UltimoGestor =
        Error.Conflito("Equipe.UltimoGestor", "A funerária precisa de ao menos um gestor ativo.");
}
