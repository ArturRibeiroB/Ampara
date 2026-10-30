using Funeraria.Modules.Financeiro.Domain.Cobranca;

namespace Funeraria.Modules.Financeiro.IntegrationTests;

/// <summary>Regras de data e de valor da cobrança (COB-01, COB-03, COB-08, COB-09, DEC-12).</summary>
public sealed class CicloCobrancaTests
{
    [Theory]
    [InlineData("2026-10-30", 10, "2026-12-05")] // venceria em 10/11 (11 dias): o próximo vencimento é 10/12.
    [InlineData("2026-10-01", 28, "2026-10-23")] // 27 dias até o vencimento: mantém este mês.
    [InlineData("2026-12-20", 5, "2027-01-31")] // vira o ano: 05/02, com a fatura emitida 5 dias antes.
    public void Mensal_emite_antes_do_proximo_vencimento_com_folga_da_primeira_cobranca(string hoje, int dia, string esperado) =>
        Assert.Equal(DateOnly.Parse(esperado), CicloCobranca.PrimeiraAncora(DateOnly.Parse(hoje), dia, PeriodicidadeCobranca.Mensal));

    [Fact]
    public void Anual_emite_um_ano_depois() =>
        Assert.Equal(new DateOnly(2027, 10, 5), CicloCobranca.PrimeiraAncora(new DateOnly(2026, 10, 30), 10, PeriodicidadeCobranca.Anual));

    [Theory]
    [InlineData("2026-10-30", 1, "2026-11-03")] // sexta; 02/11 é feriado: conta na terça.
    [InlineData("2026-12-24", 1, "2026-12-28")] // 25/12 é sexta e feriado; depois, o fim de semana.
    [InlineData("2027-03-25", 1, "2027-03-29")] // 26/03/2027 é Sexta-feira Santa.
    [InlineData("2026-10-26", 2, "2026-10-28")]
    public void Tolerancia_conta_so_dias_uteis(string vencimento, int dias, string esperado) =>
        Assert.Equal(DateOnly.Parse(esperado), CicloCobranca.FimDaTolerancia(DateOnly.Parse(vencimento), dias));

    [Theory]
    [InlineData(69.90, 5, 73.40)] // 73,395: meio para cima (exemplo do card), onde o padrão do .NET daria 73,40 só por acaso de par.
    [InlineData(10.10, 5, 10.61)] // 10,605: o arredondamento para o par daria 10,60.
    public void Reajuste_arredonda_com_meio_para_cima(double valor, double percentual, double esperado) =>
        Assert.Equal((decimal)esperado, CicloCobranca.Reajustar((decimal)valor, (decimal)percentual));

    [Fact]
    public void Encargos_somam_multa_e_juros_proporcionais() =>
        Assert.Equal(3.00m, CicloCobranca.Encargos(100m, 2m, 1m, 30));

    [Fact]
    public void Atraso_de_fatura_em_compensacao_no_vencimento_nao_e_cobrado()
    {
        var vencimento = new DateOnly(2026, 10, 10);
        Fatura fatura = Fatura.Espelhar(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new FaturaEspelhada(
            "in_1", vencimento, 100m, vencimento, SituacaoFaturaStripe.Aberta, null, null, null, Avulsa: false));

        Assert.True(fatura.AtrasoCobravel(vencimento.AddDays(3)));

        fatura.MarcarProcessando(vencimento);

        Assert.False(fatura.AtrasoCobravel(vencimento.AddDays(3)));
        Assert.False(fatura.AtrasoCobravel(vencimento));
    }
}
