using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Application.Abstractions;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Dependentes;
using Funeraria.Modules.Contratos.Contracts;

namespace Funeraria.Modules.Clientes.Application.Commands.Dependentes;

/// <summary>Edita um dependente do titular logado (CLI-02). O CPF não muda: identifica a pessoa.</summary>
public sealed record AtualizarDependenteCommand(
    Guid IdDependente,
    string Nome,
    DateOnly? DataNascimento,
    string Parentesco,
    string? Telefone,
    string? Email)
    : ICommand<DependenteResponse>, IDadosDependente;

internal sealed class AtualizarDependenteCommandValidator : AbstractValidator<AtualizarDependenteCommand>
{
    public AtualizarDependenteCommandValidator(TimeProvider timeProvider) => RegrasDependente.Aplicar(this, timeProvider);
}

public sealed class AtualizarDependenteCommandHandler(
    IDependenteRepository dependentes,
    IClientesUnitOfWork unitOfWork,
    ICurrentUser currentUser)
    : ICommandHandler<AtualizarDependenteCommand, DependenteResponse>
{
    public async ValueTask<Result<DependenteResponse>> Handle(AtualizarDependenteCommand command, CancellationToken cancellationToken)
    {
        // Dependente de outro titular responde 404, como se não existisse (CLI-02).
        Dependente? dependente = currentUser.IdCliente is Guid idCliente
            ? await dependentes.ObterDoTitularAsync(command.IdDependente, idCliente, cancellationToken)
            : null;

        if (dependente is null)
        {
            return DependenteErros.NaoEncontrado;
        }

        dependente.Atualizar(command.Nome, command.DataNascimento!.Value, command.Parentesco, command.Telefone, command.Email);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DependenteResponse.De(dependente);
    }
}

/// <summary>Remove (soft delete) um dependente do titular logado, se ele não estiver em contrato ativo (CLI-02).</summary>
public sealed record RemoverDependenteCommand(Guid IdDependente) : ICommand;

public sealed class RemoverDependenteCommandHandler(
    IDependenteRepository dependentes,
    IClientesUnitOfWork unitOfWork,
    IContratosApi contratos,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<RemoverDependenteCommand>
{
    public async ValueTask<Result> Handle(RemoverDependenteCommand command, CancellationToken cancellationToken)
    {
        Dependente? dependente = currentUser.IdCliente is Guid idCliente
            ? await dependentes.ObterDoTitularAsync(command.IdDependente, idCliente, cancellationToken)
            : null;

        if (dependente is null)
        {
            return DependenteErros.NaoEncontrado;
        }

        if (await contratos.DependentePossuiContratoAtivoAsync(dependente.Id, cancellationToken))
        {
            return DependenteErros.VinculadoAContratoAtivo;
        }

        dependente.Remover(timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
