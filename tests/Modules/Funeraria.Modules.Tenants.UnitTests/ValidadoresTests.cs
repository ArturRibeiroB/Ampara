using FluentValidation.Results;
using Funeraria.Modules.Tenants.Application.Commands.AtualizarConfiguracoes;
using Funeraria.Modules.Tenants.Application.Commands.CadastrarFuneraria;
using Funeraria.Modules.Tenants.Domain.Funerarias;

namespace Funeraria.Modules.Tenants.UnitTests;

public sealed class ValidadoresTests
{
    [Theory]
    [InlineData("paz-eterna", true)]
    [InlineData("abc", true)]
    [InlineData("ab", false)]
    [InlineData("-paz", false)]
    [InlineData("paz-", false)]
    [InlineData("Paz", false)]
    [InlineData("paz_eterna", false)]
    [InlineData("api", false)]
    [InlineData("admin", false)]
    public void Slug(string slug, bool valido)
    {
        ValidationResult result = new CadastrarFunerariaCommandValidator().Validate(Cadastro() with { Slug = slug });

        Assert.Equal(valido, !result.Errors.Any(e => e.PropertyName == nameof(CadastrarFunerariaCommand.Slug)));
    }

    [Fact]
    public void Cadastro_valido_aceita_cnpj_alfanumerico() =>
        Assert.True(new CadastrarFunerariaCommandValidator().Validate(Cadastro() with { Cnpj = "12.ABC.345/01DE-35" }).IsValid);

    [Fact]
    public void Cadastro_com_cnpj_invalido_usa_a_mensagem_do_card()
    {
        ValidationResult result = new CadastrarFunerariaCommandValidator().Validate(Cadastro() with { Cnpj = "11.222.333/0001-80" });

        Assert.Equal("CNPJ não informado ou em formato inválido.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Fact]
    public void Multa_acima_de_2_por_cento_e_recusada()
    {
        var parametros = new ParametrosFuneraria { Cobranca = new ParametrosCobranca { MultaPercentual = 2.5m } };

        ValidationResult result = new AtualizarConfiguracoesCommandValidator().Validate(
            new AtualizarConfiguracoesCommand(new IdentidadeVisual { NomeExibicao = "Paz" }, parametros));

        Assert.Equal("Parametros.Cobranca.MultaPercentual", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Escolha_do_cliente_exige_dias_validos_e_sem_repeticao()
    {
        var parametros = new ParametrosFuneraria
        {
            Cobranca = new ParametrosCobranca { ModoVencimento = ModoVencimento.EscolhaCliente, DiasVencimentoPermitidos = [5, 5, 31] }
        };

        ValidationResult result = new AtualizarConfiguracoesCommandValidator().Validate(
            new AtualizarConfiguracoesCommand(new IdentidadeVisual { NomeExibicao = "Paz" }, parametros));

        Assert.Equal("Parametros.Cobranca.DiasVencimentoPermitidos", Assert.Single(result.Errors).PropertyName);
    }

    [Fact]
    public void Identidade_visual_valida_cor_uf_e_cep()
    {
        var visual = new IdentidadeVisual
        {
            NomeExibicao = "Paz",
            CorPrimaria = "azul",
            Endereco = new Endereco { Uf = "XX", Cep = "123" }
        };

        ValidationResult result = new AtualizarConfiguracoesCommandValidator().Validate(
            new AtualizarConfiguracoesCommand(visual, new ParametrosFuneraria()));

        string[] campos = result.Errors.Select(e => e.PropertyName).ToArray();
        Assert.Contains("IdentidadeVisual.CorPrimaria", campos);
        Assert.Contains("IdentidadeVisual.Endereco.Uf", campos);
        Assert.Contains("IdentidadeVisual.Endereco.Cep", campos);
    }

    private static CadastrarFunerariaCommand Cadastro() => new(
        "Paz Eterna", "Paz Eterna Ltda", "11.222.333/0001-81", "paz-eterna", "contato@paz.com.br",
        "(11) 3333-4444", "Maria", "maria@paz.com.br", OrigemCadastro.Auto);
}
