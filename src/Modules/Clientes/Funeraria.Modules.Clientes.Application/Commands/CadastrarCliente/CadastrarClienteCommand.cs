using FluentValidation;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.Modules.Clientes.Application.Validacao;
using Funeraria.Modules.Clientes.Domain;

namespace Funeraria.Modules.Clientes.Application.Commands.CadastrarCliente;

/// <summary>
/// Cadastro do cliente titular na funerária do endereço acessado (card Criar Cliente, AJU-01).
/// Cria a conta na plataforma com a senha informada ou, se o e-mail já tiver conta (cliente de outra funerária,
/// DEC-02), confere a senha dessa conta e só cria o vínculo com esta funerária.
/// </summary>
public sealed record CadastrarClienteCommand(
    string Nome,
    string Cpf,
    DateOnly? DataNascimento,
    string Email,
    string Senha,
    string? Telefone,
    string? Endereco,
    string? Cep,
    string? Cidade,
    string? Estado,
    bool AceiteTermos = false)
    : ICommand<ClienteResponse>;

internal sealed class CadastrarClienteCommandValidator : AbstractValidator<CadastrarClienteCommand>
{
    public CadastrarClienteCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Nome não informado.")
            .MaximumLength(150).WithMessage("Nome deve ter no máximo 150 caracteres.");

        RuleFor(c => c.Cpf).Cpf().WithMessage("CPF não informado ou em formato inválido.");

        RuleFor(c => c.DataNascimento)
            .Must(d => DataNascimento.Valida(d, timeProvider)).WithMessage("Data de nascimento inválida.");

        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("E-mail em formato inválido.")
            .EmailAddress().WithMessage("E-mail em formato inválido.")
            .MaximumLength(254).WithMessage("E-mail em formato inválido.");

        RuleFor(c => c.Senha).NotEmpty().WithMessage(ClienteErros.MensagemSenha);

        RuleFor(c => c.Telefone)
            .Must(t => string.IsNullOrWhiteSpace(t) || RegrasComuns.TelefoneValido(t)).WithMessage("Telefone em formato inválido.");

        RuleFor(c => c.Cep)
            .Must(cep => string.IsNullOrWhiteSpace(cep) || RegrasComuns.CepValido(cep)).WithMessage("CEP em formato inválido.");

        RuleFor(c => c.Estado)
            .Must(uf => string.IsNullOrWhiteSpace(uf) || RegrasComuns.UfValida(uf)).WithMessage("Estado inválido. Informe a UF com duas letras.");

        RuleFor(c => c.Endereco).MaximumLength(200).WithMessage("Endereço deve ter no máximo 200 caracteres.");
        RuleFor(c => c.Cidade).MaximumLength(100).WithMessage("Cidade deve ter no máximo 100 caracteres.");
    }
}
