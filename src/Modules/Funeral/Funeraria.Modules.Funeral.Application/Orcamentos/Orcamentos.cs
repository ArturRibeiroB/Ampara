using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Financeiro.Contracts;
using Funeraria.Modules.Funeral.Application.Abstractions;
using Funeraria.Modules.Funeral.Application.Funerais;
using Funeraria.Modules.Funeral.Application.Itens;
using Funeraria.Modules.Funeral.Domain.Funerais;
using Microsoft.Extensions.Logging;
using FuneralEntity = Funeraria.Modules.Funeral.Domain.Funerais.Funeral;

namespace Funeraria.Modules.Funeral.Application.Orcamentos;

/// <summary>Monta o orçamento com os itens adicionais que ainda não foram aprovados (FUN-04).</summary>
public sealed record GerarOrcamentoCommand(Guid IdFuneral) : ICommand<OrcamentoResponse>;

/// <summary>Orçamento mais recente do funeral.</summary>
public sealed record ObterOrcamentoQuery(Guid IdFuneral) : IQuery<OrcamentoResponse>;

/// <summary>Registra a aprovação do responsável e emite a fatura (FUN-04).</summary>
public sealed record AprovarOrcamentoCommand(Guid IdFuneral, Guid? IdOrcamento, string? NomeResponsavel) : ICommand<OrcamentoResponse>;

/// <param name="IdOrcamento">Orçamento.</param>
/// <param name="Status">PENDENTE, APROVADO, SUBSTITUIDO ou CANCELADO.</param>
/// <param name="Itens">O que está sendo cobrado.</param>
/// <param name="Total">Soma dos itens.</param>
/// <param name="GeradoEm">Quando foi gerado.</param>
/// <param name="AprovadoPor">Responsável pela família que aprovou.</param>
/// <param name="AprovadoEm">Quando foi aprovado.</param>
/// <param name="IdFatura">Fatura emitida. Veja <c>GET /faturas/{id}</c>.</param>
/// <param name="LinkPagamento">Página de pagamento hospedada pelo Stripe.</param>
/// <param name="FaturaPendente">Aprovado, mas sem fatura: a funerária ainda não recebe pelo Stripe ou ele falhou. Aprovar de novo tenta outra vez.</param>
public sealed record OrcamentoResponse(
    Guid IdOrcamento,
    StatusOrcamento Status,
    IReadOnlyList<LinhaOrcamento> Itens,
    decimal Total,
    DateTime GeradoEm,
    string? AprovadoPor,
    DateTime? AprovadoEm,
    Guid? IdFatura,
    string? LinkPagamento,
    bool FaturaPendente)
{
    internal static OrcamentoResponse De(Orcamento o) =>
        new(o.Id, o.Status, o.Itens, o.Total, o.GeradoEm, o.AprovadoPor, o.AprovadoEm, o.IdFatura, o.LinkPagamento,
            o.Status == StatusOrcamento.Aprovado && o.IdFatura is null);
}

internal sealed class AprovarOrcamentoCommandValidator : AbstractValidator<AprovarOrcamentoCommand>
{
    public AprovarOrcamentoCommandValidator()
    {
        RuleFor(c => c.IdOrcamento).NotEmpty().WithMessage("Informe o orçamento apresentado à família.");
        RuleFor(c => c.NomeResponsavel)
            .NotEmpty().WithMessage("Informe o nome do responsável que aprovou.")
            .MaximumLength(150).WithMessage("Nome do responsável deve ter no máximo 150 caracteres.");
    }
}

public sealed class GerarOrcamentoCommandHandler(
    IFuneralRepository funerais,
    IFuneralUnitOfWork unitOfWork,
    ComposicaoDosItens composicao,
    ICurrentUser usuario,
    TimeProvider timeProvider)
    : ICommandHandler<GerarOrcamentoCommand, OrcamentoResponse>
{
    public async ValueTask<Result<OrcamentoResponse>> Handle(GerarOrcamentoCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        if (funeral is null)
        {
            return FuneralErros.NaoEncontrado;
        }

        ComposicaoItensResponse itens = await composicao.MontarAsync(funeral, cancellationToken);
        Result<Orcamento> gerado = funeral.GerarOrcamento(
            [.. itens.ItensAdicionais.Select(a => new LinhaOrcamento(a.IdFuneralItem, a.Nome, a.Variacao, a.Quantidade, a.PrecoUnitario, a.Subtotal, a.SomenteDiferencaVariacao))],
            timeProvider.GetUtcNow().UtcDateTime,
            usuario.IdUsuario);
        if (gerado.IsFailure)
        {
            return Result.Failure<OrcamentoResponse>(gerado.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrcamentoResponse.De(gerado.Value);
    }
}

public sealed class ObterOrcamentoQueryHandler(IFuneralRepository funerais, ICurrentUser usuario)
    : IQueryHandler<ObterOrcamentoQuery, OrcamentoResponse>
{
    public async ValueTask<Result<OrcamentoResponse>> Handle(ObterOrcamentoQuery query, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(query.IdFuneral, cancellationToken);
        if (funeral is null || !AcessoDaFamilia.Permitido(usuario, funeral.IdCliente))
        {
            return FuneralErros.NaoEncontrado;
        }

        Orcamento? recente = funeral.Orcamentos.MaxBy(o => o.GeradoEm);
        return recente is null ? FuneralErros.OrcamentoNaoEncontrado : OrcamentoResponse.De(recente);
    }
}

public sealed partial class AprovarOrcamentoCommandHandler(
    IFuneralRepository funerais,
    IFuneralUnitOfWork unitOfWork,
    IFinanceiroApi financeiro,
    IAuditoria auditoria,
    ICurrentUser usuario,
    TimeProvider timeProvider,
    ILogger<AprovarOrcamentoCommandHandler> logger)
    : ICommandHandler<AprovarOrcamentoCommand, OrcamentoResponse>
{
    public async ValueTask<Result<OrcamentoResponse>> Handle(AprovarOrcamentoCommand command, CancellationToken cancellationToken)
    {
        FuneralEntity? funeral = await funerais.ObterAsync(command.IdFuneral, cancellationToken);
        if (funeral is null || !AcessoDaFamilia.Permitido(usuario, funeral.IdCliente))
        {
            return FuneralErros.NaoEncontrado;
        }

        bool jaAprovado = funeral.Orcamentos.Any(o => o.Id == command.IdOrcamento && o.Status == StatusOrcamento.Aprovado);
        Result<Orcamento> aprovado = funeral.AprovarOrcamento(
            command.IdOrcamento!.Value, command.NomeResponsavel!, timeProvider.GetUtcNow().UtcDateTime, usuario.IdUsuario);
        if (aprovado.IsFailure)
        {
            return Result.Failure<OrcamentoResponse>(aprovado.Error);
        }

        Orcamento orcamento = aprovado.Value;
        if (!jaAprovado)
        {
            // A aprovação vale mesmo se a fatura não sair agora.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await auditoria.RegistrarAsync(new RegistroAuditoria(
                AcoesAuditadas.OrcamentoAprovado, "Orcamento", orcamento.Id,
                ValoresNovos: new { id_funeral = funeral.Id, total = orcamento.Total, aprovado_por = orcamento.AprovadoPor }),
                cancellationToken);
        }

        if (orcamento.IdFatura is null && funeral.Status != StatusFuneral.Cancelado)
        {
            await EmitirFaturaAsync(funeral, orcamento, cancellationToken);
        }

        return OrcamentoResponse.De(orcamento);
    }

    private async Task EmitirFaturaAsync(FuneralEntity funeral, Orcamento orcamento, CancellationToken cancellationToken)
    {
        try
        {
            // Sem o nome do falecido: o Stripe só precisa saber o que está cobrando.
            FaturaAvulsaDto? fatura = await financeiro.EmitirFaturaDoFuneralAsync(
                funeral.IdContrato, "Itens adicionais do funeral", orcamento.Total, $"orcamento-{orcamento.Id}", cancellationToken);
            if (fatura is not null)
            {
                orcamento.RegistrarFatura(fatura.IdFatura, fatura.LinkPagamento);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogFaturaNaoEmitida(logger, e, orcamento.Id);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fatura do orçamento {IdOrcamento} não emitida; aprovar de novo tenta outra vez.")]
    private static partial void LogFaturaNaoEmitida(ILogger logger, Exception erro, Guid idOrcamento);
}

/// <summary>Funeral cancelado: o orçamento pendente perde o efeito e a fatura em aberto é cancelada. A já paga fica para a funerária devolver.</summary>
internal sealed class CancelarOrcamentosDoFuneral(IFuneralRepository funerais, IFuneralUnitOfWork unitOfWork, IFinanceiroApi financeiro)
    : IDomainEventHandler<FuneralStatusAlteradoDomainEvent>
{
    public async Task Handle(FuneralStatusAlteradoDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.Atual != StatusFuneral.Cancelado || await funerais.ObterAsync(domainEvent.IdFuneral, cancellationToken) is not FuneralEntity funeral)
        {
            return;
        }

        IReadOnlyList<Guid> faturas = funeral.CancelarOrcamentos();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        foreach (Guid idFatura in faturas)
        {
            await financeiro.CancelarFaturaEmAbertoAsync(idFatura, cancellationToken);
        }
    }
}
