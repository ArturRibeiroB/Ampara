using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Application.Abstractions;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Clientes;

namespace Funeraria.Modules.Clientes.Application.Commands.AtualizarMeusDados;

/// <summary>
/// O cliente logado atualiza os próprios dados (CLI-01). CPF e data de nascimento não mudam; a troca de e-mail
/// exige nova confirmação e entra com o AUT-05. O id_cliente vem do token.
/// </summary>
public sealed record AtualizarMeusDadosCommand(
    string Nome,
    string? Telefone,
    string? Endereco,
    string? Cep,
    string? Cidade,
    string? Estado)
    : ICommand<ClienteResponse>;

internal sealed class AtualizarMeusDadosCommandValidator : AbstractValidator<AtualizarMeusDadosCommand>
{
    public AtualizarMeusDadosCommandValidator()
    {
        RuleFor(c => c.Nome)
            .NotEmpty().WithMessage("Nome não informado.")
            .MaximumLength(150).WithMessage("Nome deve ter no máximo 150 caracteres.");

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

public sealed class AtualizarMeusDadosCommandHandler(
    IClienteRepository clientes,
    IClientesUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<AtualizarMeusDadosCommand, ClienteResponse>
{
    public async ValueTask<Result<ClienteResponse>> Handle(AtualizarMeusDadosCommand command, CancellationToken cancellationToken)
    {
        Cliente? cliente = currentUser.IdCliente is Guid id ? await clientes.ObterPorIdAsync(id, cancellationToken) : null;
        if (cliente is null)
        {
            return ClienteErros.NaoEncontrado;
        }

        cliente.AtualizarDados(
            command.Nome,
            command.Telefone,
            command.Endereco,
            command.Cep is null ? null : DocumentosBrasileiros.Normalizar(command.Cep),
            command.Cidade,
            command.Estado);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ClienteResponse.De(cliente);
    }
}
