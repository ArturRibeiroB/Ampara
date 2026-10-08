using FluentValidation;
using Funeraria.Modules.Planos.Domain.Planos;

namespace Funeraria.Modules.Planos.Application.Commands;

/// <summary>Dados comuns à criação e à edição de um plano.</summary>
public interface IPlanoDados
{
    string Nome { get; }

    string? Descricao { get; }

    decimal ValorMensal { get; }

    decimal ValorAnual { get; }

    decimal ValorDependenteMensal { get; }

    decimal ValorDependenteAnual { get; }

    int CarenciaDias { get; }

    int LimiteDependentes { get; }

    bool PermiteSomenteDependente { get; }

    PlanoConfiguracoes? Configuracoes { get; }
}

internal static class PlanoDadosValidator
{
    public const int NomeTamanhoMaximo = 100;
    public const int DescricaoTamanhoMaximo = 1000;

    public static void AplicarRegras<T>(AbstractValidator<T> validator)
        where T : IPlanoDados
    {
        validator.RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Nome do plano é obrigatório.")
            .MaximumLength(NomeTamanhoMaximo).WithMessage($"Nome do plano deve ter no máximo {NomeTamanhoMaximo} caracteres.");

        validator.RuleFor(c => c.Descricao)
            .MaximumLength(DescricaoTamanhoMaximo).WithMessage($"Descrição deve ter no máximo {DescricaoTamanhoMaximo} caracteres.");

        validator.RuleFor(c => c.ValorMensal)
            .GreaterThan(0).WithMessage("Valor mensal deve ser maior que zero.");

        validator.RuleFor(c => c.ValorAnual)
            .GreaterThan(0).WithMessage("Valor anual deve ser maior que zero.");

        validator.RuleFor(c => c.ValorDependenteMensal)
            .GreaterThanOrEqualTo(0).WithMessage("Valor mensal por dependente não pode ser negativo.");

        validator.RuleFor(c => c.ValorDependenteAnual)
            .GreaterThanOrEqualTo(0).WithMessage("Valor anual por dependente não pode ser negativo.");

        validator.RuleFor(c => c.CarenciaDias)
            .GreaterThanOrEqualTo(0).WithMessage("Período de carência não pode ser negativo.");

        validator.RuleFor(c => c.LimiteDependentes)
            .GreaterThanOrEqualTo(0).WithMessage("Limite de dependentes não pode ser negativo.");

        validator.RuleFor(c => c)
            .Must(c => !c.PermiteSomenteDependente || c.LimiteDependentes > 0)
            .WithName(nameof(IPlanoDados.PermiteSomenteDependente))
            .WithMessage("Um plano só para dependentes precisa permitir ao menos um dependente.");

        validator.RuleFor(c => c.Configuracoes!)
            .SetValidator(new PlanoConfiguracoesValidator())
            .When(c => c.Configuracoes is not null);
    }
}

/// <summary>Validação da coluna jsonb configuracoes (PLA-03).</summary>
internal sealed class PlanoConfiguracoesValidator : AbstractValidator<PlanoConfiguracoes>
{
    public PlanoConfiguracoesValidator()
    {
        RuleFor(c => c.Versao)
            .Equal(PlanoConfiguracoes.VersaoAtual)
            .WithMessage($"Versão das configurações não suportada. Use a versão {PlanoConfiguracoes.VersaoAtual}.");

        RuleFor(c => c.Translado).NotNull().WithMessage("Configuração de translado é obrigatória.");
        RuleFor(c => c.Translado.KmIncluso)
            .GreaterThanOrEqualTo(0).WithMessage("Km incluso no translado não pode ser negativo.")
            .When(c => c.Translado is not null);

        RuleFor(c => c.Velorio).NotNull().WithMessage("Configuração de velório é obrigatória.");
        RuleFor(c => c.Velorio.HorasIncluidas)
            .GreaterThanOrEqualTo(0).WithMessage("Horas de velório incluídas não podem ser negativas.")
            .When(c => c.Velorio is not null);

        RuleFor(c => c.Dependentes).NotNull().WithMessage("Configuração de dependentes é obrigatória.");
        RuleFor(c => c.Dependentes.IdadeMaximaFilhos)
            .InclusiveBetween(0, 120).WithMessage("Idade máxima dos filhos deve estar entre 0 e 120 anos.")
            .When(c => c.Dependentes?.IdadeMaximaFilhos is not null);
    }
}
