using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Tenancy;
using Funeraria.Modules.Planos.Domain.Planos.Events;

namespace Funeraria.Modules.Planos.Domain.Planos;

/// <summary>
/// Plano funerário de uma funerária (PLA-01). Todo plano é oferecido em mensal e anual
/// e tem valor adicional por dependente (DEC-01).
/// </summary>
public sealed class Plano : AggregateRoot<Guid>, ITenantEntity, IAuditableEntity
{
    private Plano(Guid id, Guid tenantId)
        : base(id)
    {
        TenantId = tenantId;
    }

    private Plano()
    {
    }

    public Guid TenantId { get; private set; }

    public string Nome { get; private set; } = null!;

    public string? Descricao { get; private set; }

    public PrecosPlano Precos { get; private set; } = null!;

    /// <summary>Dias, a partir da contratação, até a cobertura valer (CON-04).</summary>
    public int CarenciaDias { get; private set; }

    public int LimiteDependentes { get; private set; }

    /// <summary>Permite contratar o plano só para dependentes, sem o titular coberto.</summary>
    public bool PermiteSomenteDependente { get; private set; }

    public PlanoConfiguracoes Configuracoes { get; private set; } = PlanoConfiguracoes.Padrao;

    /// <summary>Produto correspondente no Stripe. Preenchido pelo módulo Financeiro (COB-05).</summary>
    public string? IdStripeProduct { get; private set; }

    public bool Ativo { get; private set; }

    public static Plano Criar(
        Guid tenantId,
        string nome,
        string? descricao,
        PrecosPlano precos,
        int carenciaDias,
        int limiteDependentes,
        bool permiteSomenteDependente,
        PlanoConfiguracoes? configuracoes)
    {
        var plano = new Plano(Guid.CreateVersion7(), tenantId) { Ativo = true };
        plano.DefinirDados(nome, descricao, precos, carenciaDias, limiteDependentes, permiteSomenteDependente, configuracoes);
        plano.Raise(new PlanoCriadoDomainEvent(plano.Id));
        return plano;
    }

    /// <summary>
    /// Altera os dados do plano. Um novo valor vale só para novos contratos: os vigentes mudam apenas
    /// pelo reajuste anual (COB-08). O Financeiro reage ao evento criando um preço novo no Stripe.
    /// </summary>
    public void Atualizar(
        string nome,
        string? descricao,
        PrecosPlano precos,
        int carenciaDias,
        int limiteDependentes,
        bool permiteSomenteDependente,
        PlanoConfiguracoes? configuracoes)
    {
        bool valorAlterado = Precos != precos;

        DefinirDados(nome, descricao, precos, carenciaDias, limiteDependentes, permiteSomenteDependente, configuracoes);

        if (valorAlterado)
        {
            Raise(new ValorPlanoAlteradoDomainEvent(Id));
        }
    }

    /// <summary>Plano com contratos não pode ser excluído, só desativado. Desativado não aparece em GET /planos.</summary>
    public void AlterarStatus(bool ativo) => Ativo = ativo;

    public void VincularProdutoStripe(string idStripeProduct) => IdStripeProduct = idStripeProduct;

    public decimal CalcularValor(Periodicidade periodicidade, int quantidadeDependentes) =>
        Precos.Calcular(periodicidade, quantidadeDependentes);

    private void DefinirDados(
        string nome,
        string? descricao,
        PrecosPlano precos,
        int carenciaDias,
        int limiteDependentes,
        bool permiteSomenteDependente,
        PlanoConfiguracoes? configuracoes)
    {
        Nome = nome.Trim();
        Descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        Precos = precos;
        CarenciaDias = carenciaDias;
        LimiteDependentes = limiteDependentes;
        PermiteSomenteDependente = permiteSomenteDependente;
        Configuracoes = configuracoes ?? PlanoConfiguracoes.Padrao;
    }
}
