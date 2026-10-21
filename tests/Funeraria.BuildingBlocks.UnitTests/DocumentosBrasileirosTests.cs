using Funeraria.BuildingBlocks.Application.Validation;

namespace Funeraria.BuildingBlocks.UnitTests;

public sealed class DocumentosBrasileirosTests
{
    [Theory]
    [InlineData("529.982.247-25")]
    [InlineData("52998224725")]
    [InlineData("111.444.777-35")]
    public void Cpf_valido(string cpf) => Assert.True(DocumentosBrasileiros.CpfValido(cpf));

    [Theory]
    [InlineData("529.982.247-24")]
    [InlineData("111.111.111-11")]
    [InlineData("5299822472")]
    [InlineData("5299822472A")]
    [InlineData("")]
    [InlineData(null)]
    public void Cpf_invalido(string? cpf) => Assert.False(DocumentosBrasileiros.CpfValido(cpf));

    [Theory]
    [InlineData("11.222.333/0001-81")]
    [InlineData("11222333000181")]
    [InlineData("12.ABC.345/01DE-35")] // exemplo da Receita para o CNPJ alfanumérico (IN RFB 2.229/2024)
    [InlineData("12abc34501de35")]
    public void Cnpj_valido_numerico_e_alfanumerico(string cnpj) => Assert.True(DocumentosBrasileiros.CnpjValido(cnpj));

    [Theory]
    [InlineData("11.222.333/0001-80")]
    [InlineData("12.ABC.345/01DE-36")]
    [InlineData("12.ABC.345/01DE-3X")] // dígitos verificadores continuam numéricos
    [InlineData("00.000.000/0000-00")]
    [InlineData("1122233300018")]
    [InlineData(null)]
    public void Cnpj_invalido(string? cnpj) => Assert.False(DocumentosBrasileiros.CnpjValido(cnpj));

    [Fact]
    public void Normalizar_remove_pontuacao_e_deixa_maiusculas() =>
        Assert.Equal("12ABC34501DE35", DocumentosBrasileiros.Normalizar(" 12.abc.345/01de-35 "));

    [Theory]
    [InlineData("(11) 3333-4444", true)]
    [InlineData("11 99999-8888", true)]
    [InlineData("99999-8888", false)]
    [InlineData("(11) 99999-88880", false)]
    public void Telefone_com_ddd(string telefone, bool valido) => Assert.Equal(valido, RegrasComuns.TelefoneValido(telefone));
}
