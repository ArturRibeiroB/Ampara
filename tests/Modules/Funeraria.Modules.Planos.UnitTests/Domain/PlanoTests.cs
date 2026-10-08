using Funeraria.Modules.Planos.Domain.Planos;
using Funeraria.Modules.Planos.Domain.Planos.Events;

namespace Funeraria.Modules.Planos.UnitTests.Domain;

public sealed class PlanoTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly PrecosPlano Precos = new(100m, 1080m, 30m, 324m);

    [Fact]
    public void Criar_deve_iniciar_ativo_e_levantar_PlanoCriado()
    {
        Plano plano = CriarPlano();

        Assert.True(plano.Ativo);
        Assert.Equal(Tenant, plano.TenantId);
        PlanoCriadoDomainEvent evento = Assert.IsType<PlanoCriadoDomainEvent>(Assert.Single(plano.DomainEvents));
        Assert.Equal(plano.Id, evento.IdPlano);
    }

    [Fact]
    public void Criar_deve_normalizar_textos_e_usar_configuracao_padrao()
    {
        Plano plano = Plano.Criar(Tenant, "  Familiar  ", "   ", Precos, 90, 4, false, configuracoes: null);

        Assert.Equal("Familiar", plano.Nome);
        Assert.Null(plano.Descricao);
        Assert.Equal(PlanoConfiguracoes.Padrao, plano.Configuracoes);
    }

    [Fact]
    public void Atualizar_com_valor_novo_deve_levantar_ValorPlanoAlterado()
    {
        Plano plano = CriarPlano();
        plano.ClearDomainEvents();

        plano.Atualizar("Essencial", null, Precos with { ValorMensal = 120m }, 90, 4, false, null);

        ValorPlanoAlteradoDomainEvent evento = Assert.IsType<ValorPlanoAlteradoDomainEvent>(Assert.Single(plano.DomainEvents));
        Assert.Equal(plano.Id, evento.IdPlano);
        Assert.Equal(120m, plano.Precos.ValorMensal);
    }

    [Fact]
    public void Atualizar_sem_mudar_valores_nao_deve_levantar_evento()
    {
        Plano plano = CriarPlano();
        plano.ClearDomainEvents();

        plano.Atualizar("Essencial Plus", "Nova descrição", Precos with { }, 60, 2, false, null);

        Assert.Empty(plano.DomainEvents);
        Assert.Equal("Essencial Plus", plano.Nome);
    }

    [Theory]
    [InlineData(Periodicidade.Mensal, 0, 100)]
    [InlineData(Periodicidade.Mensal, 2, 160)]
    [InlineData(Periodicidade.Anual, 0, 1080)]
    [InlineData(Periodicidade.Anual, 3, 2052)]
    public void CalcularValor_deve_somar_titular_e_dependentes(Periodicidade periodicidade, int dependentes, decimal esperado)
    {
        Plano plano = CriarPlano();

        Assert.Equal(esperado, plano.CalcularValor(periodicidade, dependentes));
    }

    [Fact]
    public void CalcularValor_com_dependentes_negativos_deve_lancar()
    {
        Plano plano = CriarPlano();

        Assert.Throws<ArgumentOutOfRangeException>(() => plano.CalcularValor(Periodicidade.Mensal, -1));
    }

    [Fact]
    public void AlterarStatus_deve_desativar_e_reativar()
    {
        Plano plano = CriarPlano();

        plano.AlterarStatus(false);
        Assert.False(plano.Ativo);

        plano.AlterarStatus(true);
        Assert.True(plano.Ativo);
    }

    private static Plano CriarPlano() =>
        Plano.Criar(Tenant, "Essencial", "Plano básico", Precos, 90, 4, false, null);
}
