using Funeraria.BuildingBlocks.Domain.Entities;

namespace Funeraria.Modules.Tenants.Domain.Funerarias;

/// <summary>
/// Funerária cliente da plataforma: é o tenant (TEN-02). Não implementa <c>ITenantEntity</c>,
/// porque é ela que define o tenant. O id é o <c>id_tenant</c> usado em todos os módulos.
/// </summary>
public sealed class Funeraria : AggregateRoot<Guid>, IAuditableEntity
{
    private Funeraria(Guid id)
        : base(id)
    {
    }

    private Funeraria()
    {
    }

    public string NomeFantasia { get; private set; } = null!;

    public string RazaoSocial { get; private set; } = null!;

    public string Cnpj { get; private set; } = null!;

    /// <summary>Subdomínio da funerária: letras minúsculas, números e hífen.</summary>
    public string Slug { get; private set; } = null!;

    public string EmailContato { get; private set; } = null!;

    public string? Telefone { get; private set; }

    public StatusFuneraria Status { get; private set; }

    public OrigemCadastro OrigemCadastro { get; private set; }

    public string? IdStripeAccount { get; private set; }

    /// <summary>
    /// Cria a funerária. As validações de CNPJ, slug e nomes reservados ficam no caso de uso de cadastro (TEN-02).
    /// </summary>
    public static Funeraria Criar(
        string nomeFantasia,
        string razaoSocial,
        string cnpj,
        string slug,
        string emailContato,
        string? telefone,
        OrigemCadastro origem,
        StatusFuneraria status = StatusFuneraria.Pendente) =>
        new(Guid.CreateVersion7())
        {
            NomeFantasia = nomeFantasia,
            RazaoSocial = razaoSocial,
            Cnpj = cnpj,
            Slug = slug.ToLowerInvariant(),
            EmailContato = emailContato,
            Telefone = telefone,
            OrigemCadastro = origem,
            Status = status
        };
}

public enum StatusFuneraria
{
    Pendente = 1,
    Ativa = 2,
    Suspensa = 3
}

public enum OrigemCadastro
{
    Auto = 1,
    Consultor = 2,
    Admin = 3
}
