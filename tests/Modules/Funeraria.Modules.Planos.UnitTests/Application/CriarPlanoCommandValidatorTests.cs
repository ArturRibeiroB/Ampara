using FluentValidation.Results;
using Funeraria.Modules.Planos.Application.Commands.CriarPlano;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.UnitTests.Application;

public sealed class CriarPlanoCommandValidatorTests
{
    private readonly CriarPlanoCommandValidator _validator = new();

    [Fact]
    public void Comando_valido_deve_passar()
    {
        ValidationResult result = _validator.Validate(ComandoValido());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Nome_vazio_deve_falhar_com_mensagem_em_portugues()
    {
        ValidationResult result = _validator.Validate(ComandoValido() with { Nome = "" });

        ValidationFailure erro = Assert.Single(result.Errors);
        Assert.Equal(nameof(CriarPlanoCommand.Nome), erro.PropertyName);
        Assert.Equal("Nome do plano é obrigatório.", erro.ErrorMessage);
    }

    [Fact]
    public void Valores_invalidos_devem_falhar()
    {
        ValidationResult result = _validator.Validate(ComandoValido() with
        {
            ValorMensal = 0,
            ValorAnual = -1,
            ValorDependenteMensal = -1,
            CarenciaDias = -1,
            LimiteDependentes = -1
        });

        string[] campos = result.Errors.Select(e => e.PropertyName).Distinct().ToArray();
        Assert.Contains(nameof(CriarPlanoCommand.ValorMensal), campos);
        Assert.Contains(nameof(CriarPlanoCommand.ValorAnual), campos);
        Assert.Contains(nameof(CriarPlanoCommand.ValorDependenteMensal), campos);
        Assert.Contains(nameof(CriarPlanoCommand.CarenciaDias), campos);
        Assert.Contains(nameof(CriarPlanoCommand.LimiteDependentes), campos);
    }

    [Fact]
    public void Plano_somente_dependente_sem_limite_deve_falhar()
    {
        ValidationResult result = _validator.Validate(ComandoValido() with
        {
            PermiteSomenteDependente = true,
            LimiteDependentes = 0
        });

        ValidationFailure erro = Assert.Single(result.Errors);
        Assert.Equal(nameof(CriarPlanoCommand.PermiteSomenteDependente), erro.PropertyName);
    }

    [Fact]
    public void Configuracoes_com_versao_desconhecida_devem_falhar()
    {
        var configuracoes = new PlanoConfiguracoes { Versao = 99 };

        ValidationResult result = _validator.Validate(ComandoValido() with { Configuracoes = configuracoes });

        ValidationFailure erro = Assert.Single(result.Errors);
        Assert.Equal("Configuracoes.Versao", erro.PropertyName);
    }

    [Fact]
    public void Configuracoes_com_km_negativo_devem_falhar()
    {
        var configuracoes = new PlanoConfiguracoes { Translado = new ConfiguracaoTranslado { KmIncluso = -10 } };

        ValidationResult result = _validator.Validate(ComandoValido() with { Configuracoes = configuracoes });

        ValidationFailure erro = Assert.Single(result.Errors);
        Assert.Equal("Configuracoes.Translado.KmIncluso", erro.PropertyName);
    }

    private static CriarPlanoCommand ComandoValido() => new(
        Nome: "Essencial",
        Descricao: null,
        ValorMensal: 89.90m,
        ValorAnual: 970.92m,
        ValorDependenteMensal: 19.90m,
        ValorDependenteAnual: 214.92m,
        CarenciaDias: 90,
        LimiteDependentes: 4,
        PermiteSomenteDependente: false,
        Configuracoes: null);
}
