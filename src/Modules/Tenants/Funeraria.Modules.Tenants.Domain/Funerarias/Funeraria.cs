using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Domain.Funerarias.Events;

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

    /// <summary>Só letras e números, sem pontuação. Pode ser alfanumérico (IN RFB 2.229/2024).</summary>
    public string Cnpj { get; private set; } = null!;

    /// <summary>Subdomínio da funerária: letras minúsculas, números e hífen.</summary>
    public string Slug { get; private set; } = null!;

    public string EmailContato { get; private set; } = null!;

    public string? Telefone { get; private set; }

    public StatusFuneraria Status { get; private set; }

    public OrigemCadastro OrigemCadastro { get; private set; }

    /// <summary>Conta conectada no Stripe. Preenchida no onboarding de pagamentos (DEC-09).</summary>
    public string? IdStripeAccount { get; private set; }

    /// <summary>Como a funerária aparece para os clientes (TEN-03).</summary>
    public IdentidadeVisual IdentidadeVisual { get; private set; } = new();

    /// <summary>Parâmetros do negócio, com os padrões do DEC-01 (TEN-03).</summary>
    public ParametrosFuneraria Parametros { get; private set; } = new();

    /// <summary>
    /// Cadastro de uma funerária nova (TEN-02). Começa PENDENTE e convida o gestor: os outros módulos
    /// reagem ao <see cref="FunerariaCriadaDomainEvent"/>.
    /// </summary>
    public static Funeraria Cadastrar(
        string nomeFantasia,
        string razaoSocial,
        string cnpj,
        string slug,
        string emailContato,
        string? telefone,
        OrigemCadastro origem,
        string gestorNome,
        string gestorEmail)
    {
        Funeraria funeraria = Criar(nomeFantasia, razaoSocial, cnpj, slug, emailContato, telefone, origem);
        funeraria.Raise(new FunerariaCriadaDomainEvent(funeraria.Id, gestorNome.Trim(), gestorEmail.Trim()));
        return funeraria;
    }

    /// <summary>Cria a funerária sem convidar ninguém. Usado pelo seed e pelos testes.</summary>
    public static Funeraria Criar(
        string nomeFantasia,
        string razaoSocial,
        string cnpj,
        string slug,
        string emailContato,
        string? telefone,
        OrigemCadastro origem,
        StatusFuneraria status = StatusFuneraria.Pendente)
    {
        var funeraria = new Funeraria(Guid.CreateVersion7())
        {
            NomeFantasia = nomeFantasia.Trim(),
            RazaoSocial = razaoSocial.Trim(),
            Cnpj = cnpj,
            Slug = slug.Trim().ToLowerInvariant(),
            EmailContato = emailContato.Trim(),
            Telefone = string.IsNullOrWhiteSpace(telefone) ? null : telefone.Trim(),
            OrigemCadastro = origem,
            Status = status
        };

        funeraria.IdentidadeVisual = new IdentidadeVisual
        {
            NomeExibicao = funeraria.NomeFantasia,
            Email = funeraria.EmailContato,
            Telefone = funeraria.Telefone
        };

        return funeraria;
    }

    /// <summary>
    /// Transições permitidas: PENDENTE → ATIVA ou SUSPENSA; ATIVA ↔ SUSPENSA.
    /// Funerária suspensa não permite login dos seus usuários nem novas contratações (TEN-02).
    /// </summary>
    public Result AlterarStatus(StatusFuneraria novo)
    {
        if (novo == Status)
        {
            return Result.Success();
        }

        bool permitida = (Status, novo) switch
        {
            (StatusFuneraria.Pendente, StatusFuneraria.Ativa) => true,
            (StatusFuneraria.Pendente, StatusFuneraria.Suspensa) => true,
            (StatusFuneraria.Ativa, StatusFuneraria.Suspensa) => true,
            (StatusFuneraria.Suspensa, StatusFuneraria.Ativa) => true,
            _ => false
        };

        if (!permitida)
        {
            return FunerariaErros.TransicaoInvalida(Status, novo);
        }

        Status = novo;
        return Result.Success();
    }

    /// <summary>
    /// Mudanças nos parâmetros de cobrança valem para novos contratos e para as próximas faturas.
    /// Faturas já emitidas não mudam (TEN-03).
    /// </summary>
    public void AtualizarConfiguracoes(IdentidadeVisual identidadeVisual, ParametrosFuneraria parametros)
    {
        IdentidadeVisual = identidadeVisual;
        Parametros = parametros;
    }
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
