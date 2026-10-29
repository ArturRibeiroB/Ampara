using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Contratos.Application.Abstractions;
using Funeraria.Modules.Contratos.Domain.Contratos;
using Funeraria.Modules.Funeral.Contracts;
using Funeraria.Modules.Planos.Contracts;

namespace Funeraria.Modules.Contratos.Application.Commands.AlterarContrato;

/// <summary>Inclui um dependente do titular num contrato existente (CON-02). Carência a partir de hoje; valor novo na próxima fatura.</summary>
public sealed record IncluirDependenteContratoCommand(Guid IdContrato, Guid? IdDependente) : ICommand<ContratoResponse>;

/// <summary>Tira um dependente do contrato (CON-02). Quem já foi registrado como beneficiário não sai.</summary>
public sealed record RemoverDependenteContratoCommand(Guid IdContrato, Guid IdDependente) : ICommand<ContratoResponse>;

/// <summary>Cancela o contrato pelo titular ou pela equipe interna, com motivo (CON-03).</summary>
public sealed record CancelarContratoCommand(Guid IdContrato, string? Motivo) : ICommand<ContratoResponse>;

internal sealed class IncluirDependenteContratoCommandValidator : AbstractValidator<IncluirDependenteContratoCommand>
{
    public IncluirDependenteContratoCommandValidator() =>
        RuleFor(c => c.IdDependente).NotEmpty().WithMessage(ContratoErros.DependenteInvalido);
}

internal sealed class CancelarContratoCommandValidator : AbstractValidator<CancelarContratoCommand>
{
    public CancelarContratoCommandValidator() =>
        RuleFor(c => c.Motivo)
            .NotEmpty().WithMessage("Informe o motivo do cancelamento.")
            .MaximumLength(500).WithMessage("Motivo deve ter no máximo 500 caracteres.");
}

public sealed class IncluirDependenteContratoCommandHandler(
    IContratoRepository contratos,
    IContratosUnitOfWork unitOfWork,
    IClientesApi clientes,
    IPlanosApi planos,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<IncluirDependenteContratoCommand, ContratoResponse>
{
    public async ValueTask<Result<ContratoResponse>> Handle(IncluirDependenteContratoCommand command, CancellationToken cancellationToken)
    {
        Contrato? contrato = await AcessoAoContrato.ObterAsync(contratos, usuario, command.IdContrato, cancellationToken);
        if (contrato is null)
        {
            return ContratoErros.NaoEncontrado;
        }

        Guid idDependente = command.IdDependente!.Value;
        DependenteDto? dependente = (await clientes.ObterDependentesAsync([idDependente], cancellationToken)).FirstOrDefault();
        if (dependente is null)
        {
            return ContratoErros.DependenteNaoEncontrado;
        }

        if (dependente.IdCliente != contrato.IdCliente)
        {
            return ContratoErros.DependenteDeOutroCliente;
        }

        if ((await contratos.DependentesCobertosAsync([idDependente], cancellationToken)).Count > 0)
        {
            return ContratoErros.DependenteEmContratoVigente;
        }

        // O limite é o do plano hoje: o contrato guarda preço e carência da contratação, não o limite.
        PlanoDto? plano = await planos.ObterPlanoAsync(contrato.IdPlano, cancellationToken);
        if (plano is not null && contrato.QuantidadeDependentes + 1 > plano.LimiteDependentes)
        {
            return ContratoErros.LimiteDependentes;
        }

        decimal valorAnterior = contrato.ValorTotal;
        Result<PessoaCoberta> incluida = contrato.IncluirDependente(idDependente, DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
        if (incluida.IsFailure)
        {
            return Result.Failure<ContratoResponse>(incluida.Error);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            return ContratoErros.DependenteEmContratoVigente;
        }

        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.ContratoDependenteIncluido, "Contrato", contrato.Id,
            ValoresAnteriores: new { valor_total = valorAnterior },
            ValoresNovos: new { id_dependente = idDependente, valor_total = contrato.ValorTotal }), cancellationToken);

        return ContratoResponse.De(contrato);
    }
}

public sealed class RemoverDependenteContratoCommandHandler(
    IContratoRepository contratos,
    IContratosUnitOfWork unitOfWork,
    IFuneralApi funeral,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<RemoverDependenteContratoCommand, ContratoResponse>
{
    public async ValueTask<Result<ContratoResponse>> Handle(RemoverDependenteContratoCommand command, CancellationToken cancellationToken)
    {
        Contrato? contrato = await AcessoAoContrato.ObterAsync(contratos, usuario, command.IdContrato, cancellationToken);
        if (contrato is null)
        {
            return ContratoErros.NaoEncontrado;
        }

        PessoaCoberta? pessoa = contrato.PessoasCobertas.FirstOrDefault(p => p.IdDependente == command.IdDependente && p.DataRemocao is null);
        if (pessoa is not null && await funeral.PessoaEhBeneficiariaAsync(pessoa.Id, cancellationToken))
        {
            return ContratoErros.DependenteBeneficiario;
        }

        decimal valorAnterior = contrato.ValorTotal;
        Result removida = contrato.RemoverDependente(command.IdDependente, DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
        if (removida.IsFailure)
        {
            return Result.Failure<ContratoResponse>(removida.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.ContratoDependenteRemovido, "Contrato", contrato.Id,
            ValoresAnteriores: new { id_dependente = command.IdDependente, valor_total = valorAnterior },
            ValoresNovos: new { valor_total = contrato.ValorTotal }), cancellationToken);

        return ContratoResponse.De(contrato);
    }
}

public sealed class CancelarContratoCommandHandler(
    IContratoRepository contratos,
    IContratosUnitOfWork unitOfWork,
    IFuneralApi funeral,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<CancelarContratoCommand, ContratoResponse>
{
    public async ValueTask<Result<ContratoResponse>> Handle(CancelarContratoCommand command, CancellationToken cancellationToken)
    {
        if (usuario.Perfil == Perfis.Consultor)
        {
            return ContratoErros.CancelamentoNaoPermitido;
        }

        Contrato? contrato = await AcessoAoContrato.ObterAsync(contratos, usuario, command.IdContrato, cancellationToken);
        if (contrato is null)
        {
            return ContratoErros.NaoEncontrado;
        }

        if (await funeral.PossuiFuneralEmAndamentoAsync(contrato.Id, cancellationToken))
        {
            return ContratoErros.FuneralEmAndamento;
        }

        StatusContrato statusAnterior = contrato.Status;
        Result cancelado = contrato.Cancelar(command.Motivo!, DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
        if (cancelado.IsFailure)
        {
            return Result.Failure<ContratoResponse>(cancelado.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.ContratoCancelado, "Contrato", contrato.Id,
            ValoresAnteriores: new { status = statusAnterior.ToString() },
            ValoresNovos: new { status = contrato.Status.ToString(), motivo = contrato.MotivoCancelamento }), cancellationToken);

        return ContratoResponse.De(contrato);
    }
}

internal static class AcessoAoContrato
{
    /// <summary>A equipe altera qualquer contrato da funerária; o cliente, só os próprios (os outros dão 404, CON-01).</summary>
    public static async Task<Contrato?> ObterAsync(
        IContratoRepository contratos, ICurrentUser usuario, Guid idContrato, CancellationToken cancellationToken)
    {
        Contrato? contrato = await contratos.ObterAsync(idContrato, cancellationToken);
        return contrato is not null && (usuario.Perfil != Perfis.Cliente || contrato.IdCliente == usuario.IdCliente) ? contrato : null;
    }
}
