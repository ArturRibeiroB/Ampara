using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Application.Abstractions;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;

namespace Funeraria.Modules.Clientes.Application.Commands.Dependentes;

/// <summary>
/// O titular logado cadastra um dependente (card Criar Dependente). O id_cliente vem do token: o request não
/// tem como associar o dependente a outro titular.
/// </summary>
public sealed record CadastrarDependenteCommand(
    string Nome,
    string Cpf,
    DateOnly? DataNascimento,
    string Parentesco,
    string? Telefone,
    string? Email)
    : ICommand<DependenteResponse>, IDadosDependente;

internal sealed class CadastrarDependenteCommandValidator : AbstractValidator<CadastrarDependenteCommand>
{
    public CadastrarDependenteCommandValidator(TimeProvider timeProvider)
    {
        RuleFor(d => d.Cpf)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(DocumentosBrasileiros.CpfValido).WithMessage("CPF inválido.")
            .When(d => !string.IsNullOrWhiteSpace(d.Cpf), ApplyConditionTo.CurrentValidator);

        RegrasDependente.Aplicar(this, timeProvider);
    }
}

public sealed class CadastrarDependenteCommandHandler(
    IClienteRepository clientes,
    IDependenteRepository dependentes,
    IClientesUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<CadastrarDependenteCommand, DependenteResponse>
{
    public async ValueTask<Result<DependenteResponse>> Handle(CadastrarDependenteCommand command, CancellationToken cancellationToken)
    {
        Cliente? titular = currentUser.IdCliente is Guid id ? await clientes.ObterPorIdAsync(id, cancellationToken) : null;
        if (titular is null)
        {
            return ClienteErros.NaoEncontrado;
        }

        string cpf = DocumentosBrasileiros.Normalizar(command.Cpf);
        if (cpf == titular.Cpf)
        {
            return DependenteErros.CpfDoTitular;
        }

        // O mesmo dependente não entra duas vezes para o mesmo titular (card Criar Dependente).
        if (await dependentes.ExisteCpfDoTitularAsync(titular.Id, cpf, cancellationToken))
        {
            return DependenteErros.CpfDuplicado;
        }

        var dependente = Dependente.Criar(
            titular.TenantId,
            titular.Id,
            command.Nome,
            cpf,
            command.DataNascimento!.Value,
            command.Parentesco,
            command.Telefone,
            command.Email);

        dependentes.Adicionar(dependente);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DependenteResponse.De(dependente);
    }
}
