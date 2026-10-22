using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Tenancy;

namespace Funeraria.Modules.Clientes.Domain.Dependentes;

/// <summary>
/// Dependente cadastrado pelo cliente titular (card Criar Dependente, CLI-02). Pode ser incluído depois num contrato.
/// A remoção é lógica, para preservar o histórico de contratos e funerais.
/// </summary>
public sealed class Dependente : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Dependente(Guid id)
        : base(id)
    {
    }

    private Dependente()
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>Titular. Vem sempre do token, nunca do request.</summary>
    public Guid IdCliente { get; private set; }

    public string Nome { get; private set; } = null!;

    /// <summary>Só dígitos. Não muda depois do cadastro: identifica a pessoa.</summary>
    public string Cpf { get; private set; } = null!;

    public DateOnly DataNascimento { get; private set; }

    /// <summary>Texto livre, como no card: "Esposa", "Filho", "Mãe".</summary>
    public string Parentesco { get; private set; } = null!;

    public string? Telefone { get; private set; }

    public string? Email { get; private set; }

    public DateTime? RemovidoEm { get; private set; }

    public bool Removido => RemovidoEm is not null;

    public static Dependente Criar(
        Guid tenantId,
        Guid idCliente,
        string nome,
        string cpf,
        DateOnly dataNascimento,
        string parentesco,
        string? telefone,
        string? email)
    {
        var dependente = new Dependente(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            IdCliente = idCliente,
            Cpf = cpf
        };

        dependente.Atualizar(nome, dataNascimento, parentesco, telefone, email);
        return dependente;
    }

    public void Atualizar(string nome, DateOnly dataNascimento, string parentesco, string? telefone, string? email)
    {
        Nome = nome.Trim();
        DataNascimento = dataNascimento;
        Parentesco = parentesco.Trim();
        Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    /// <summary>Remoção lógica (CLI-02). Quem chama confere antes se há contrato ativo.</summary>
    public void Remover(DateTime agora) => RemovidoEm ??= agora;
}
