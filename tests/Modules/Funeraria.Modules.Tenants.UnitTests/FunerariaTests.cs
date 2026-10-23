using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Funeraria.Modules.Tenants.Domain.Funerarias.Events;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.UnitTests;

public sealed class FunerariaTests
{
    [Fact]
    public void Cadastro_nasce_pendente_com_os_padroes_do_DEC01_e_DEC12_e_convida_o_gestor()
    {
        FunerariaEntity f = Cadastrar();

        Assert.Equal(StatusFuneraria.Pendente, f.Status);
        Assert.Equal("paz-eterna", f.Slug);
        Assert.Equal("Paz Eterna", f.IdentidadeVisual.NomeExibicao);
        Assert.Equal(ModoVencimento.Fixo, f.Parametros.Cobranca.ModoVencimento);
        Assert.Equal(10, f.Parametros.Cobranca.DiaVencimentoFixo);
        Assert.Equal(1, f.Parametros.Cobranca.ToleranciaDias);
        Assert.Equal(5, f.Parametros.Cobranca.ReajusteAnualPercentual);
        Assert.Equal(2, f.Parametros.Cobranca.MultaPercentual);
        Assert.Equal(1, f.Parametros.Cobranca.JurosMensalPercentual);
        Assert.Equal(10, f.Parametros.Cobranca.DescontoAnualPercentual);
        Assert.True(f.Parametros.Cobranca.EstaCompleta());

        FunerariaCriadaDomainEvent evento = Assert.IsType<FunerariaCriadaDomainEvent>(Assert.Single(f.DomainEvents));
        Assert.Equal(f.Id, evento.IdTenant);
        Assert.Equal("gestor@paz.com.br", evento.GestorEmail);
    }

    [Theory]
    [InlineData(StatusFuneraria.Pendente, StatusFuneraria.Ativa)]
    [InlineData(StatusFuneraria.Pendente, StatusFuneraria.Suspensa)]
    [InlineData(StatusFuneraria.Ativa, StatusFuneraria.Suspensa)]
    [InlineData(StatusFuneraria.Suspensa, StatusFuneraria.Ativa)]
    [InlineData(StatusFuneraria.Ativa, StatusFuneraria.Ativa)]
    public void Transicoes_permitidas(StatusFuneraria de, StatusFuneraria para)
    {
        FunerariaEntity f = ComStatus(de);

        Assert.True(f.AlterarStatus(para).IsSuccess);
        Assert.Equal(para, f.Status);
    }

    [Theory]
    [InlineData(StatusFuneraria.Ativa, StatusFuneraria.Pendente)]
    [InlineData(StatusFuneraria.Suspensa, StatusFuneraria.Pendente)]
    public void Funeraria_nao_volta_para_pendente(StatusFuneraria de, StatusFuneraria para)
    {
        FunerariaEntity f = ComStatus(de);

        Result result = f.AlterarStatus(para);

        Assert.True(result.IsFailure);
        Assert.Equal("Funeraria.TransicaoInvalida", result.Error.Code);
        Assert.Equal(de, f.Status);
    }

    [Fact]
    public void Cobranca_so_fica_completa_com_multa_juros_e_desconto()
    {
        var parcial = new ParametrosCobranca { DescontoAnualPercentual = null };

        Assert.False(parcial.EstaCompleta());
        Assert.True((parcial with { DescontoAnualPercentual = 10 }).EstaCompleta());
    }

    [Fact]
    public void Parametros_com_os_mesmos_dias_sao_iguais() =>
        Assert.Equal(
            new ParametrosCobranca { DiasVencimentoPermitidos = [5, 10] },
            new ParametrosCobranca { DiasVencimentoPermitidos = [5, 10] });

    private static FunerariaEntity Cadastrar() => FunerariaEntity.Cadastrar(
        " Paz Eterna ", "Paz Eterna Serviços Póstumos Ltda", "11222333000181", "Paz-Eterna",
        "contato@paz.com.br", null, OrigemCadastro.Auto, "Maria Gestora", "gestor@paz.com.br");

    private static FunerariaEntity ComStatus(StatusFuneraria status) => FunerariaEntity.Criar(
        "Paz", "Paz Ltda", "11222333000181", "paz", "contato@paz.com.br", null, OrigemCadastro.Admin, status);
}
