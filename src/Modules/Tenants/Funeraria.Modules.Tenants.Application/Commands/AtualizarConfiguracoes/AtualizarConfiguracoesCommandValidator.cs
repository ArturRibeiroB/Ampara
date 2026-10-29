using System.Text.RegularExpressions;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.Modules.Tenants.Domain.Funerarias;

namespace Funeraria.Modules.Tenants.Application.Commands.AtualizarConfiguracoes;

internal sealed class AtualizarConfiguracoesCommandValidator : AbstractValidator<AtualizarConfiguracoesCommand>
{
    public AtualizarConfiguracoesCommandValidator()
    {
        RuleFor(c => c.IdentidadeVisual).NotNull().WithMessage("Identidade visual é obrigatória.")
            .SetValidator(new IdentidadeVisualValidator()!);

        RuleFor(c => c.Parametros).NotNull().WithMessage("Parâmetros são obrigatórios.")
            .SetValidator(new ParametrosFunerariaValidator()!);
    }
}

internal sealed partial class IdentidadeVisualValidator : AbstractValidator<IdentidadeVisual>
{
    public IdentidadeVisualValidator()
    {
        RuleFor(v => v.NomeExibicao)
            .NotEmpty().WithMessage("Nome de exibição é obrigatório.")
            .MaximumLength(150).WithMessage("Nome de exibição deve ter no máximo 150 caracteres.");

        RuleFor(v => v.CorPrimaria).Must(Cor).WithMessage("Cor primária deve estar no formato #RRGGBB.");
        RuleFor(v => v.CorSecundaria).Must(Cor).WithMessage("Cor secundária deve estar no formato #RRGGBB.");

        RuleFor(v => v.Email)
            .EmailAddress().WithMessage("E-mail inválido.")
            .MaximumLength(254).WithMessage("E-mail deve ter no máximo 254 caracteres.");

        RuleFor(v => v.Telefone).Telefone();
        RuleFor(v => v.Whatsapp).Telefone();
        RuleFor(v => v.HorarioAtendimento).MaximumLength(200).WithMessage("Horário de atendimento deve ter no máximo 200 caracteres.");

        RuleFor(v => v.Endereco).NotNull().WithMessage("Endereço é obrigatório.").SetValidator(new EnderecoValidator());
    }

    private static bool Cor(string? cor) => cor is not null && CorHex().IsMatch(cor);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex CorHex();
}

internal sealed class ParametrosFunerariaValidator : AbstractValidator<ParametrosFuneraria>
{
    // Até o dia 28 para o vencimento existir em todos os meses.
    private const int UltimoDia = 28;

    public ParametrosFunerariaValidator()
    {
        RuleFor(p => p.Cobranca).NotNull().WithMessage("Parâmetros de cobrança são obrigatórios.");
        RuleFor(p => p.Lembretes).NotNull().WithMessage("Parâmetros de lembretes são obrigatórios.");
        RuleFor(p => p.Terceiros).NotNull().WithMessage("Parâmetros dos terceiros são obrigatórios.");
        RuleFor(p => p.Certidao).NotNull().WithMessage("Parâmetros da certidão são obrigatórios.");
        RuleFor(p => p.Certidao.Modo).IsInEnum().WithMessage("Modo da certidão inválido. Use MANUAL ou SIMULADO.").When(p => p.Certidao is not null);

        When(p => p.Cobranca is not null, () =>
        {
            RuleFor(p => p.Cobranca.ModoVencimento).IsInEnum().WithMessage("Modo de vencimento inválido. Use FIXO ou ESCOLHA_CLIENTE.");

            RuleFor(p => p.Cobranca.DiaVencimentoFixo)
                .InclusiveBetween(1, UltimoDia).WithMessage($"Dia de vencimento deve estar entre 1 e {UltimoDia}.");

            RuleFor(p => p.Cobranca.DiasVencimentoPermitidos)
                .NotEmpty().WithMessage("Informe ao menos um dia de vencimento para o cliente escolher.")
                .Must(d => d.All(dia => dia is >= 1 and <= UltimoDia) && d.Distinct().Count() == d.Count)
                .WithMessage($"Dias de vencimento devem estar entre 1 e {UltimoDia}, sem repetição.")
                .When(p => p.Cobranca.ModoVencimento == ModoVencimento.EscolhaCliente);

            RuleFor(p => p.Cobranca.ToleranciaDias)
                .InclusiveBetween(0, 30).WithMessage("Tolerância deve estar entre 0 e 30 dias.");

            // Limite de 2% do Código de Defesa do Consumidor (art. 52, § 1º), confirmado no DEC-12.
            RuleFor(p => p.Cobranca.MultaPercentual)
                .InclusiveBetween(0, 2).WithMessage("Multa deve estar entre 0% e 2% (limite do CDC).");

            RuleFor(p => p.Cobranca.JurosMensalPercentual)
                .InclusiveBetween(0, 10).WithMessage("Juros devem estar entre 0% e 10% ao mês.");

            RuleFor(p => p.Cobranca.DescontoAnualPercentual)
                .InclusiveBetween(0, 100).WithMessage("Desconto anual deve estar entre 0% e 100%.");

            RuleFor(p => p.Cobranca.ReajusteAnualPercentual)
                .InclusiveBetween(0, 100).WithMessage("Reajuste anual deve estar entre 0% e 100%.");
        });

        When(p => p.Lembretes is not null, () =>
        {
            RuleFor(p => p.Lembretes.DiasAntes)
                .Must(d => d.All(dia => dia is >= 1 and <= 30)).WithMessage("Lembretes antes do vencimento: de 1 a 30 dias.");
            RuleFor(p => p.Lembretes.DiasDepois)
                .Must(d => d.All(dia => dia is >= 1 and <= 30)).WithMessage("Lembretes depois do vencimento: de 1 a 30 dias.");
        });

        When(p => p.Terceiros is not null, () =>
            RuleFor(p => p.Terceiros.PrazoAceiteHoras)
                .InclusiveBetween(1, 168).WithMessage("Prazo para o terceiro responder: de 1 a 168 horas (7 dias)."));
    }
}
