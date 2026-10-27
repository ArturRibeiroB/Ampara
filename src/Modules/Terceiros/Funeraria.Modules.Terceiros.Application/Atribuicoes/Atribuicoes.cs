using System.Text.Json;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Terceiros.Application.Abstractions;
using Funeraria.Modules.Terceiros.Application.Cadastro;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;
using Funeraria.Modules.Terceiros.Domain.Atribuicoes;
using Funeraria.Modules.Terceiros.Domain.Terceiros;

namespace Funeraria.Modules.Terceiros.Application.Atribuicoes;

/// <summary>Atribuições criadas pelos módulos de origem (Translado, Documentos, Funeral), TER-02.</summary>
public sealed class AtribuicoesTerceiros(
    ITerceiroRepository terceiros,
    IAtribuicaoRepository atribuicoes,
    ITerceirosUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : ITerceirosAtribuicoes
{
    public async Task<Result<Guid>> AtribuirAsync(NovaAtribuicao atribuicao, CancellationToken cancellationToken = default)
    {
        TipoAtribuicao tipo = atribuicao.Resumo switch
        {
            ResumoTranslado => TipoAtribuicao.Translado,
            ResumoCertidao => TipoAtribuicao.Certidao,
            ResumoPedido => TipoAtribuicao.Pedido,
            _ => throw new ArgumentException("Tipo de resumo desconhecido.", nameof(atribuicao))
        };

        Terceiro? terceiro = await terceiros.ObterAsync(atribuicao.IdTerceiro, cancellationToken);
        if (terceiro is null)
        {
            return Result.Failure<Guid>(TerceiroErros.NaoEncontrado);
        }

        if (terceiro.Status == StatusTerceiro.Inativo)
        {
            return Result.Failure<Guid>(AtribuicaoErros.TerceiroIndisponivel);
        }

        if (terceiro.Tipo != Atribuicao.TerceiroDoTipo(tipo))
        {
            return Result.Failure<Guid>(AtribuicaoErros.TipoIncompativel);
        }

        if (await atribuicoes.ObterPorOrigemAsync(terceiro.Id, tipo, atribuicao.IdOrigem, cancellationToken) is Atribuicao existente)
        {
            return existente.Id;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        var nova = Atribuicao.Criar(
            tenantContext.RequiredTenantId,
            terceiro.Id,
            tipo,
            atribuicao.IdOrigem,
            JsonSerializer.Serialize(atribuicao.Resumo, atribuicao.Resumo.GetType(), JsonbOptions.Default),
            atribuicao.PrazoAceite ?? agora + Atribuicao.PrazoPadrao);

        atribuicoes.Adicionar(nova);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return nova.Id;
    }

    public async Task<Result> EncerrarAsync(Guid idAtribuicao, bool concluida, CancellationToken cancellationToken = default)
    {
        Atribuicao? atribuicao = await atribuicoes.ObterAsync(idAtribuicao, cancellationToken);
        if (atribuicao is null)
        {
            return AtribuicaoErros.NaoEncontrada;
        }

        atribuicao.Encerrar(concluida);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Cadastro do terceiro logado (TER-02).</summary>
public sealed record ObterMeuCadastroQuery : IQuery<TerceiroResponse>;

/// <summary>
/// Lista única do terceiro logado: translados, certidões e pedidos (TER-02). Status: PENDENTE, ACEITA, RECUSADA,
/// EXPIRADA, CONCLUIDA ou CANCELADA.
/// </summary>
public sealed record ListarMinhasAtribuicoesQuery(string? Status) : IQuery<IReadOnlyList<AtribuicaoResponse>>;

public sealed record AceitarAtribuicaoCommand(Guid IdAtribuicao) : ICommand<AtribuicaoResponse>;

public sealed record RecusarAtribuicaoCommand(Guid IdAtribuicao, string Motivo) : ICommand<AtribuicaoResponse>;

/// <param name="IdAtribuicao">Atribuição.</param>
/// <param name="Tipo">TRANSLADO, CERTIDAO ou PEDIDO.</param>
/// <param name="IdOrigem">Registro no módulo de origem, usado nos endpoints de cada serviço (TRA-04, CER-04, FUN-09).</param>
/// <param name="Status">Status atual; pendente com prazo vencido aparece como EXPIRADA.</param>
/// <param name="PrazoAceite">Até quando dá para aceitar ou recusar.</param>
/// <param name="RespondidaEm">Quando o terceiro respondeu.</param>
/// <param name="MotivoRecusa">Motivo informado na recusa.</param>
/// <param name="Resumo">Só os dados que o terceiro pode ver, conforme o tipo de serviço.</param>
public sealed record AtribuicaoResponse(
    Guid IdAtribuicao,
    TipoAtribuicao Tipo,
    Guid IdOrigem,
    StatusAtribuicao Status,
    DateTime PrazoAceite,
    DateTime? RespondidaEm,
    string? MotivoRecusa,
    JsonElement Resumo)
{
    public static AtribuicaoResponse De(Atribuicao a, DateTime agora)
    {
        using var resumo = JsonDocument.Parse(a.Resumo);
        return new(a.Id, a.Tipo, a.IdOrigem, a.StatusEm(agora), a.PrazoAceite, a.RespondidaEm, a.MotivoRecusa, resumo.RootElement.Clone());
    }
}

internal sealed class ListarMinhasAtribuicoesQueryValidator : AbstractValidator<ListarMinhasAtribuicoesQuery>
{
    public ListarMinhasAtribuicoesQueryValidator() =>
        RuleFor(q => q.Status).Must(s => s is null || FiltroEnum.Ler<StatusAtribuicao>(s) is not null)
            .WithMessage("Status inválido. Use PENDENTE, ACEITA, RECUSADA, EXPIRADA, CONCLUIDA ou CANCELADA.");
}

internal sealed class RecusarAtribuicaoCommandValidator : AbstractValidator<RecusarAtribuicaoCommand>
{
    public RecusarAtribuicaoCommandValidator() =>
        RuleFor(c => c.Motivo)
            .NotEmpty().WithMessage("Informe o motivo da recusa.")
            .MaximumLength(500).WithMessage("Motivo deve ter no máximo 500 caracteres.");
}

public sealed class ObterMeuCadastroQueryHandler(ITerceiroRepository terceiros, ICurrentUser usuario)
    : IQueryHandler<ObterMeuCadastroQuery, TerceiroResponse>
{
    public async ValueTask<Result<TerceiroResponse>> Handle(ObterMeuCadastroQuery query, CancellationToken cancellationToken) =>
        usuario.IdTerceiro is Guid id && await terceiros.ObterAsync(id, cancellationToken) is Terceiro t
            ? TerceiroResponse.De(t)
            : TerceiroErros.NaoEncontrado;
}

public sealed class ListarMinhasAtribuicoesQueryHandler(IAtribuicaoRepository atribuicoes, ICurrentUser usuario, TimeProvider timeProvider)
    : IQueryHandler<ListarMinhasAtribuicoesQuery, IReadOnlyList<AtribuicaoResponse>>
{
    public async ValueTask<Result<IReadOnlyList<AtribuicaoResponse>>> Handle(ListarMinhasAtribuicoesQuery query, CancellationToken cancellationToken)
    {
        if (usuario.IdTerceiro is not Guid idTerceiro)
        {
            return Result.Failure<IReadOnlyList<AtribuicaoResponse>>(TerceiroErros.NaoEncontrado);
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        StatusAtribuicao? status = FiltroEnum.Ler<StatusAtribuicao>(query.Status);
        return (await atribuicoes.ListarDoTerceiroAsync(idTerceiro, cancellationToken))
            .Where(a => status is null || a.StatusEm(agora) == status)
            .Select(a => AtribuicaoResponse.De(a, agora))
            .ToList();
    }
}

public sealed class AceitarAtribuicaoCommandHandler(
    IAtribuicaoRepository atribuicoes,
    ITerceirosUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<AceitarAtribuicaoCommand, AtribuicaoResponse>
{
    public async ValueTask<Result<AtribuicaoResponse>> Handle(AceitarAtribuicaoCommand command, CancellationToken cancellationToken)
    {
        Atribuicao? atribuicao = await DoTerceiro.ObterAsync(atribuicoes, usuario, command.IdAtribuicao, cancellationToken);
        if (atribuicao is null)
        {
            return AtribuicaoErros.NaoEncontrada;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        Result aceita = atribuicao.Aceitar(agora);
        if (aceita.IsFailure)
        {
            return Result.Failure<AtribuicaoResponse>(aceita.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.AtribuicaoAceita, "Atribuicao", atribuicao.Id,
            ValoresNovos: new { tipo = atribuicao.Tipo.ToString(), atribuicao.IdOrigem }), cancellationToken);
        return AtribuicaoResponse.De(atribuicao, agora);
    }
}

public sealed class RecusarAtribuicaoCommandHandler(
    IAtribuicaoRepository atribuicoes,
    ITerceirosUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    IAuditoria auditoria)
    : ICommandHandler<RecusarAtribuicaoCommand, AtribuicaoResponse>
{
    public async ValueTask<Result<AtribuicaoResponse>> Handle(RecusarAtribuicaoCommand command, CancellationToken cancellationToken)
    {
        Atribuicao? atribuicao = await DoTerceiro.ObterAsync(atribuicoes, usuario, command.IdAtribuicao, cancellationToken);
        if (atribuicao is null)
        {
            return AtribuicaoErros.NaoEncontrada;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        Result recusada = atribuicao.Recusar(agora, command.Motivo);
        if (recusada.IsFailure)
        {
            return Result.Failure<AtribuicaoResponse>(recusada.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await auditoria.RegistrarAsync(new RegistroAuditoria(
            AcoesAuditadas.AtribuicaoRecusada, "Atribuicao", atribuicao.Id,
            ValoresNovos: new { tipo = atribuicao.Tipo.ToString(), atribuicao.IdOrigem, motivo = atribuicao.MotivoRecusa }), cancellationToken);
        return AtribuicaoResponse.De(atribuicao, agora);
    }
}

/// <summary>Publica a resposta do terceiro para o módulo de origem (TER-02).</summary>
internal sealed class AtribuicaoRespondidaDomainEventHandler(IAtribuicaoRepository atribuicoes, IEventBus eventBus)
    : IDomainEventHandler<AtribuicaoRespondidaDomainEvent>
{
    public async Task Handle(AtribuicaoRespondidaDomainEvent evento, CancellationToken cancellationToken)
    {
        if (await atribuicoes.ObterAsync(evento.IdAtribuicao, cancellationToken) is not Atribuicao a)
        {
            return;
        }

        string tipo = a.Tipo.ToString().ToUpperInvariant();
        if (evento.Aceita)
        {
            await eventBus.PublishAsync(new AtribuicaoAceitaIntegrationEvent(a.TenantId, a.Id, a.IdTerceiro, tipo, a.IdOrigem), cancellationToken);
        }
        else
        {
            await eventBus.PublishAsync(
                new AtribuicaoRecusadaIntegrationEvent(a.TenantId, a.Id, a.IdTerceiro, tipo, a.IdOrigem, a.MotivoRecusa ?? string.Empty),
                cancellationToken);
        }
    }
}

internal static class DoTerceiro
{
    /// <summary>Só a atribuição do próprio terceiro; a de outro dá 404, como se não existisse.</summary>
    public static async Task<Atribuicao?> ObterAsync(IAtribuicaoRepository atribuicoes, ICurrentUser usuario, Guid id, CancellationToken cancellationToken) =>
        usuario.IdTerceiro is Guid idTerceiro && await atribuicoes.ObterAsync(id, cancellationToken) is Atribuicao a && a.IdTerceiro == idTerceiro
            ? a
            : null;
}
