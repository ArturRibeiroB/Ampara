using System.Text.RegularExpressions;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.Modules.Tenants.Contracts;
using Funeraria.Modules.Tenants.Domain.Funerarias;

namespace Funeraria.Modules.Tenants.Application.Commands.CadastrarFuneraria;

/// <summary>
/// Cadastro de funerária (TEN-02): auto-cadastro pela landing ou pelo ADMIN. Nos dois casos a funerária nasce
/// PENDENTE e o gestor recebe por e-mail o convite para definir a senha, que também confirma o e-mail.
/// </summary>
public sealed record CadastrarFunerariaCommand(
    string NomeFantasia,
    string RazaoSocial,
    string Cnpj,
    string Slug,
    string EmailContato,
    string? Telefone,
    string GestorNome,
    string GestorEmail,
    OrigemCadastro Origem)
    : ICommand<FunerariaCadastradaResponse>;

public sealed record FunerariaCadastradaResponse(Guid IdTenant, string Slug, StatusFuneraria Status);

internal sealed partial class CadastrarFunerariaCommandValidator : AbstractValidator<CadastrarFunerariaCommand>
{
    public CadastrarFunerariaCommandValidator()
    {
        RuleFor(c => c.NomeFantasia)
            .NotEmpty().WithMessage("Nome fantasia é obrigatório.")
            .MaximumLength(150).WithMessage("Nome fantasia deve ter no máximo 150 caracteres.");

        RuleFor(c => c.RazaoSocial)
            .NotEmpty().WithMessage("Razão social é obrigatória.")
            .MaximumLength(150).WithMessage("Razão social deve ter no máximo 150 caracteres.");

        RuleFor(c => c.Cnpj).Cnpj();

        RuleFor(c => c.Slug)
            .NotEmpty().WithMessage("Endereço (slug) é obrigatório.")
            .Must(s => SlugValido().IsMatch(s ?? string.Empty))
            .WithMessage("Endereço deve ter de 3 a 63 caracteres: letras minúsculas, números e hífen, sem começar ou terminar com hífen.")
            .Must(s => !SubdominiosReservados.Contem(s ?? string.Empty))
            .WithMessage("Este endereço é reservado pela plataforma. Escolha outro.");

        RuleFor(c => c.EmailContato).EmailObrigatorio();
        RuleFor(c => c.Telefone).Telefone();

        RuleFor(c => c.GestorNome)
            .NotEmpty().WithMessage("Nome do gestor é obrigatório.")
            .MaximumLength(150).WithMessage("Nome do gestor deve ter no máximo 150 caracteres.");

        RuleFor(c => c.GestorEmail).EmailObrigatorio();
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{1,61}[a-z0-9])$")]
    private static partial Regex SlugValido();
}
