using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Contracts;
using Funeraria.Modules.Contratos.Application.Abstractions;
using Funeraria.Modules.Contratos.Domain.Contratos;
using Funeraria.Modules.Planos.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Contratos.Application.Commands.CriarContrato;

/// <summary>
/// Cria o contrato de um titular com um plano e registra quem tem cobertura (card Criar Contrato).
/// O cliente contrata para si (o titular vem do token); a equipe da funerária informa o titular.
/// </summary>
/// <remarks>
/// Os dependentes precisam estar cadastrados para o titular. O dia de vencimento só é usado quando a funerária deixa
/// o cliente escolher; no modo FIXO vale o dia configurado pela funerária.
/// </remarks>
public sealed record CriarContratoCommand(
    Guid? IdCliente,
    Guid? IdPlano,
    TipoCobertura? TipoCobertura,
    IReadOnlyList<Guid>? Dependentes,
    Periodicidade? Periodicidade,
    int? DiaVencimento)
    : ICommand<ContratoResponse>;

internal sealed class CriarContratoCommandValidator : AbstractValidator<CriarContratoCommand>
{
    public CriarContratoCommandValidator()
    {
        RuleFor(c => c.IdCliente).NotEmpty().WithMessage(ContratoErros.TitularObrigatorio);
        RuleFor(c => c.IdPlano).NotEmpty().WithMessage(ContratoErros.PlanoObrigatorio);
        RuleFor(c => c.TipoCobertura).NotNull().WithMessage(ContratoErros.TipoCoberturaObrigatorio)
            .IsInEnum().WithMessage(ContratoErros.TipoCoberturaObrigatorio);
        RuleFor(c => c.Periodicidade).NotNull().WithMessage("Periodicidade é obrigatória.")
            .IsInEnum().WithMessage("Periodicidade inválida. Use MENSAL ou ANUAL.");

        // Id vazio, repetido ou dependente numa cobertura só do titular.
        RuleFor(c => c.Dependentes)
            .Must(d => d is null || (d.All(id => id != Guid.Empty) && d.Distinct().Count() == d.Count))
            .WithMessage(ContratoErros.DependenteInvalido);
        RuleFor(c => c.Dependentes)
            .Must(d => d is null || d.Count == 0).WithMessage(ContratoErros.DependenteInvalido)
            .When(c => c.TipoCobertura == TipoCobertura.Titular);
        RuleFor(c => c.Dependentes)
            .NotEmpty().WithMessage("Selecione ao menos um dependente.")
            .When(c => c.TipoCobertura is TipoCobertura.TitularEDependentes or TipoCobertura.SomenteDependentes);
    }
}

public sealed class CriarContratoCommandHandler(
    IContratoRepository contratos,
    IContratosUnitOfWork unitOfWork,
    IClientesApi clientes,
    IPlanosApi planos,
    ITenantsApi tenants,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : ICommandHandler<CriarContratoCommand, ContratoResponse>
{
    public async ValueTask<Result<ContratoResponse>> Handle(CriarContratoCommand command, CancellationToken cancellationToken)
    {
        Guid idTenant = tenantContext.RequiredTenantId;
        Guid idCliente = command.IdCliente!.Value;
        TipoCobertura tipoCobertura = command.TipoCobertura!.Value;
        Periodicidade periodicidade = command.Periodicidade!.Value;
        Guid[] idsDependentes = [.. command.Dependentes ?? []];

        ParametrosCobrancaDto? cobranca = await tenants.ObterParametrosCobrancaAsync(idTenant, cancellationToken);
        if (cobranca is not { ContratacaoLiberada: true })
        {
            return ContratoErros.ContratacaoIndisponivel;
        }

        TitularDto? titular = await clientes.ObterTitularAsync(idCliente, cancellationToken);
        if (titular is null)
        {
            return ContratoErros.TitularNaoEncontrado;
        }

        // Plano inativo não está disponível para contratação: responde como se não existisse, igual à consulta pública.
        PlanoDto? plano = await planos.ObterPlanoAsync(command.IdPlano!.Value, cancellationToken);
        if (plano is not { Ativo: true })
        {
            return ContratoErros.PlanoNaoEncontrado;
        }

        if (tipoCobertura == TipoCobertura.SomenteDependentes && !plano.PermiteSomenteDependente)
        {
            return ContratoErros.SomenteDependentesNaoPermitido;
        }

        Result dependentes = await ConferirDependentesAsync(idCliente, idsDependentes, plano, cancellationToken);
        if (dependentes.IsFailure)
        {
            return Result.Failure<ContratoResponse>(dependentes.Error);
        }

        Result<int> diaVencimento = DiaVencimento(cobranca, command.DiaVencimento);
        if (diaVencimento.IsFailure)
        {
            return Result.Failure<ContratoResponse>(diaVencimento.Error);
        }

        if (await contratos.ExisteVigenteAsync(idCliente, plano.IdPlano, cancellationToken))
        {
            return ContratoErros.ContratoVigenteNoPlano;
        }

        PlanoContratado contratado = periodicidade == Periodicidade.Anual
            ? new(plano.IdPlano, plano.Nome, plano.ValorAnual, plano.ValorDependenteAnual, plano.CarenciaDias)
            : new(plano.IdPlano, plano.Nome, plano.ValorMensal, plano.ValorDependenteMensal, plano.CarenciaDias);

        var contrato = Contrato.Contratar(
            idTenant,
            idCliente,
            contratado,
            tipoCobertura,
            periodicidade,
            diaVencimento.Value,
            idsDependentes,
            DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));

        contratos.Adicionar(contrato);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException e)
        {
            // Duas contratações ao mesmo tempo: os índices únicos garantem a regra que a consulta acima não cobre.
            return e.Restricao?.Contains("pessoas_cobertas", StringComparison.Ordinal) == true
                ? ContratoErros.DependenteEmContratoVigente
                : ContratoErros.ContratoVigenteNoPlano;
        }

        return ContratoResponse.De(contrato);
    }

    private async Task<Result> ConferirDependentesAsync(
        Guid idCliente,
        Guid[] idsDependentes,
        PlanoDto plano,
        CancellationToken cancellationToken)
    {
        if (idsDependentes.Length == 0)
        {
            return Result.Success();
        }

        IReadOnlyList<DependenteDto> encontrados = await clientes.ObterDependentesAsync(idsDependentes, cancellationToken);
        if (encontrados.Count != idsDependentes.Length)
        {
            return ContratoErros.DependenteNaoEncontrado;
        }

        if (encontrados.Any(d => d.IdCliente != idCliente))
        {
            return ContratoErros.DependenteDeOutroCliente;
        }

        if (idsDependentes.Length > plano.LimiteDependentes)
        {
            return ContratoErros.LimiteDependentes;
        }

        if ((await contratos.DependentesCobertosAsync(idsDependentes, cancellationToken)).Count > 0)
        {
            return ContratoErros.DependenteEmContratoVigente;
        }

        return Result.Success();
    }

    /// <summary>Modo FIXO: o dia da funerária. ESCOLHA_CLIENTE: um dos dias permitidos (DEC-01, TEN-03).</summary>
    private static Result<int> DiaVencimento(ParametrosCobrancaDto cobranca, int? escolhido)
    {
        if (!cobranca.VencimentoEscolhidoPeloCliente)
        {
            return cobranca.DiaVencimentoFixo;
        }

        return escolhido is int dia && cobranca.DiasVencimentoPermitidos.Contains(dia)
            ? dia
            : Result.Failure<int>(ContratoErros.DiaVencimentoNaoPermitido(cobranca.DiasVencimentoPermitidos));
    }
}
