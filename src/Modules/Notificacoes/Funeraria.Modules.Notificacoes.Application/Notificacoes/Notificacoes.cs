using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Paginacao;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Notificacoes.Domain.Notificacoes;
using Funeraria.Modules.Terceiros.Contracts.IntegrationEvents;

namespace Funeraria.Modules.Notificacoes.Application.Notificacoes;

public interface INotificacoesUnitOfWork : IUnitOfWork;

/// <summary>Entrega na hora para quem está conectado (SignalR). Quem não estiver vê na central depois.</summary>
public interface IEntregaTempoReal
{
    Task EnviarAsync(Guid idUsuario, NotificacaoResponse notificacao, CancellationToken cancellationToken);
}

public sealed record NotificacaoResponse(
    Guid IdNotificacao,
    string Tipo,
    string Titulo,
    string Mensagem,
    string? Link,
    DateTime CriadaEm,
    DateTime? LidaEm)
{
    public static NotificacaoResponse De(Notificacao n) => new(n.Id, n.Tipo, n.Titulo, n.Mensagem, n.Link, n.CriadaEm, n.LidaEm);
}

/// <summary>Página da central com o total de não lidas (o número no sino).</summary>
public sealed record NotificacoesResponse(IReadOnlyList<NotificacaoResponse> Itens, int Pagina, int Tamanho, long Total, long NaoLidas);

/// <summary>Central de notificações do usuário logado (NOT-02).</summary>
public sealed record MinhasNotificacoesQuery(PaginaRequest Pagina, bool SomenteNaoLidas) : IQuery<NotificacoesResponse>;

public sealed record MarcarNotificacaoLidaCommand(Guid IdNotificacao) : ICommand;

public sealed record MarcarTodasLidasCommand : ICommand;

/// <summary>Grava uma notificação por destinatário e entrega na hora para quem está conectado.</summary>
public sealed class ServicoNotificacoes(
    INotificacaoRepository notificacoes,
    INotificacoesUnitOfWork unitOfWork,
    IEntregaTempoReal tempoReal,
    TimeProvider timeProvider)
    : INotificacoes
{
    public async Task NotificarAsync(NovaNotificacao notificacao, CancellationToken cancellationToken = default)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        Notificacao[] criadas = [.. notificacao.IdsUsuarios.Distinct().Select(id => Notificacao.Criar(
            id, notificacao.IdTenant, notificacao.Tipo, notificacao.Titulo, notificacao.Mensagem, notificacao.Link, agora))];

        foreach (Notificacao n in criadas)
        {
            notificacoes.Adicionar(n);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (Notificacao n in criadas)
        {
            await tempoReal.EnviarAsync(n.IdUsuario, NotificacaoResponse.De(n), cancellationToken);
        }
    }
}

public sealed class MinhasNotificacoesQueryHandler(INotificacaoRepository notificacoes, ICurrentUser usuario, ITenantContext tenantContext)
    : IQueryHandler<MinhasNotificacoesQuery, NotificacoesResponse>
{
    public async ValueTask<Result<NotificacoesResponse>> Handle(MinhasNotificacoesQuery query, CancellationToken cancellationToken)
    {
        PaginaRequest pagina = query.Pagina.Normalizada();
        (IReadOnlyList<Notificacao> itens, long total, long naoLidas) = await notificacoes.ListarAsync(
            usuario.IdUsuario!.Value, tenantContext.TenantId, query.SomenteNaoLidas, pagina.Deslocamento, pagina.Tamanho, cancellationToken);
        return new NotificacoesResponse([.. itens.Select(NotificacaoResponse.De)], pagina.Pagina, pagina.Tamanho, total, naoLidas);
    }
}

public sealed class MarcarNotificacaoLidaCommandHandler(
    INotificacaoRepository notificacoes,
    INotificacoesUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider timeProvider)
    : ICommandHandler<MarcarNotificacaoLidaCommand>
{
    public async ValueTask<Result> Handle(MarcarNotificacaoLidaCommand command, CancellationToken cancellationToken)
    {
        Notificacao? notificacao = await notificacoes.ObterDoUsuarioAsync(command.IdNotificacao, usuario.IdUsuario!.Value, cancellationToken);
        if (notificacao is null)
        {
            return NotificacaoErros.NaoEncontrada;
        }

        notificacao.MarcarLida(timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

public sealed class MarcarTodasLidasCommandHandler(
    INotificacaoRepository notificacoes,
    INotificacoesUnitOfWork unitOfWork,
    ICurrentUser usuario,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : ICommandHandler<MarcarTodasLidasCommand>
{
    public async ValueTask<Result> Handle(MarcarTodasLidasCommand command, CancellationToken cancellationToken)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        foreach (Notificacao n in await notificacoes.NaoLidasAsync(usuario.IdUsuario!.Value, tenantContext.TenantId, cancellationToken))
        {
            n.MarcarLida(agora);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Terceiro recebeu um serviço (TER-02): avisa o parceiro para aceitar ou recusar dentro do prazo.</summary>
internal sealed class AvisarTerceiroDaAtribuicao(INotificacoes notificacoes) : IIntegrationEventHandler<AtribuicaoCriadaIntegrationEvent>
{
    public Task Handle(AtribuicaoCriadaIntegrationEvent evento, CancellationToken cancellationToken) =>
        evento.IdUsuarioTerceiro is Guid idUsuario
            ? notificacoes.NotificarAsync(new NovaNotificacao(
                [idUsuario], evento.IdTenant, "ATRIBUICAO_CRIADA", $"Novo serviço: {Assunto(evento.Tipo)}",
                $"Responda até {evento.PrazoAceite:dd/MM/yyyy HH:mm} (UTC).", $"/terceiros/atribuicoes/{evento.IdAtribuicao}"), cancellationToken)
            : Task.CompletedTask;

    internal static string Assunto(string tipo) => tipo switch
    {
        "TRANSLADO" => "translado",
        "CERTIDAO" => "certidão de óbito",
        _ => "pedido de itens"
    };
}

/// <summary>O terceiro aceitou um serviço (TER-02): avisa os gestores da funerária.</summary>
internal sealed class AvisarFunerariaDoAceite(INotificacoes notificacoes, IIdentidadeApi identidade)
    : IIntegrationEventHandler<AtribuicaoAceitaIntegrationEvent>
{
    public async Task Handle(AtribuicaoAceitaIntegrationEvent evento, CancellationToken cancellationToken) =>
        await notificacoes.NotificarAsync(new NovaNotificacao(
            await identidade.ListarGestoresAtivosAsync(evento.IdTenant, cancellationToken), evento.IdTenant, "ATRIBUICAO_ACEITA",
            $"Parceiro aceitou o {AvisarTerceiroDaAtribuicao.Assunto(evento.Tipo)}", "O serviço foi aceito pelo terceiro.",
            $"/gestao/terceiros/{evento.IdTerceiro}"), cancellationToken);
}

/// <summary>O terceiro recusou um serviço (TER-02): avisa os gestores para escolherem outro parceiro.</summary>
internal sealed class AvisarFunerariaDaRecusa(INotificacoes notificacoes, IIdentidadeApi identidade)
    : IIntegrationEventHandler<AtribuicaoRecusadaIntegrationEvent>
{
    public async Task Handle(AtribuicaoRecusadaIntegrationEvent evento, CancellationToken cancellationToken) =>
        await notificacoes.NotificarAsync(new NovaNotificacao(
            await identidade.ListarGestoresAtivosAsync(evento.IdTenant, cancellationToken), evento.IdTenant, "ATRIBUICAO_RECUSADA",
            $"Parceiro recusou o {AvisarTerceiroDaAtribuicao.Assunto(evento.Tipo)}", $"Motivo: {evento.Motivo} Escolha outro parceiro.",
            $"/gestao/terceiros/{evento.IdTerceiro}"), cancellationToken);
}
