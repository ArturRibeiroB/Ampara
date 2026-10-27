using Funeraria.Modules.Contratos.Domain.Contratos;

namespace Funeraria.Modules.Contratos.UnitTests;

/// <summary>CON-04: cenários de data e de situação do contrato.</summary>
public sealed class ElegibilidadeTests
{
    private static readonly DateOnly Contratacao = new(2026, 1, 10);

    [Theory]
    [InlineData(-1, false)] // um dia antes de terminar a carência
    [InlineData(0, true)]   // no dia em que a carência termina
    [InlineData(200, true)]
    public void Carencia_conta_da_inclusao_da_pessoa(int diasDepoisDaCarencia, bool elegivel)
    {
        Contrato contrato = Ativo(carenciaDias: 90);
        PessoaCoberta titular = contrato.PessoasCobertas[0];

        Elegibilidade resultado = contrato.AvaliarElegibilidade(titular, Contratacao.AddDays(90 + diasDepoisDaCarencia));

        Assert.Equal(elegivel, resultado.Elegivel);
        Assert.Equal(new DateOnly(2026, 4, 10), resultado.CarenciaCumpridaEm);
        Assert.Equal(elegivel ? null : "Em carência até 10/04/2026.", resultado.Motivo);
    }

    [Fact]
    public void Ano_bissexto_e_virada_de_ano_nao_confundem_a_carencia()
    {
        Contrato contrato = Ativo(carenciaDias: 60, hoje: new DateOnly(2027, 12, 31));

        Elegibilidade resultado = contrato.AvaliarElegibilidade(contrato.PessoasCobertas[0], new DateOnly(2028, 2, 28));

        Assert.False(resultado.Elegivel);
        Assert.Equal(new DateOnly(2028, 2, 29), resultado.CarenciaCumpridaEm);
    }

    [Fact]
    public void Sem_carencia_vale_no_dia_da_contratacao()
    {
        Contrato contrato = Ativo(carenciaDias: 0);

        Assert.True(contrato.AvaliarElegibilidade(contrato.PessoasCobertas[0], Contratacao).Elegivel);
    }

    [Fact]
    public void Contrato_a_pagar_suspenso_ou_cancelado_nao_e_elegivel_mesmo_com_a_carencia_cumprida()
    {
        DateOnly depois = Contratacao.AddYears(1);
        Contrato aPagar = Novo(carenciaDias: 0);
        Contrato suspenso = Ativo(carenciaDias: 0);
        suspenso.Suspender();

        Assert.Equal("Contrato aguardando o pagamento da primeira cobrança.", aPagar.AvaliarElegibilidade(aPagar.PessoasCobertas[0], depois).Motivo);
        Assert.Equal("Contrato suspenso por falta de pagamento.", suspenso.AvaliarElegibilidade(suspenso.PessoasCobertas[0], depois).Motivo);
    }

    [Fact]
    public void Quitar_a_suspensao_devolve_a_cobertura_sem_nova_carencia()
    {
        Contrato contrato = Ativo(carenciaDias: 30);
        contrato.Suspender();
        contrato.Ativar();

        Elegibilidade resultado = contrato.AvaliarElegibilidade(contrato.PessoasCobertas[0], Contratacao.AddDays(31));

        Assert.True(resultado.Elegivel);
    }

    [Fact]
    public void Cada_pessoa_tem_a_propria_carencia()
    {
        Contrato contrato = Ativo(carenciaDias: 90, dependentes: 1);

        Assert.All(contrato.PessoasCobertas, p =>
            Assert.Equal(p.DataInclusao.AddDays(90), contrato.AvaliarElegibilidade(p, Contratacao).CarenciaCumpridaEm));
    }

    private static Contrato Ativo(int carenciaDias, int dependentes = 0, DateOnly? hoje = null)
    {
        Contrato contrato = Novo(carenciaDias, dependentes, hoje);
        contrato.Ativar();
        return contrato;
    }

    private static Contrato Novo(int carenciaDias, int dependentes = 0, DateOnly? hoje = null) =>
        Contrato.Contratar(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new PlanoContratado(Guid.NewGuid(), "Familiar", 89.90m, 19.90m, carenciaDias),
            dependentes == 0 ? TipoCobertura.Titular : TipoCobertura.TitularEDependentes,
            Periodicidade.Mensal,
            10,
            [.. Enumerable.Range(0, dependentes).Select(_ => Guid.NewGuid())],
            hoje ?? Contratacao);
}
