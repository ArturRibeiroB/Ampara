namespace Funeraria.Modules.Notificacoes.Contracts;

/// <summary>
/// Notificações dentro do sistema (NOT-02): gravadas para a central do usuário e enviadas na hora pelo SignalR
/// (<c>/hubs/notificacoes</c>) para quem estiver conectado.
/// </summary>
public interface INotificacoes
{
    Task NotificarAsync(NovaNotificacao notificacao, CancellationToken cancellationToken = default);
}

/// <param name="IdsUsuarios">Contas que recebem.</param>
/// <param name="IdTenant">Funerária do assunto; vazio para avisos da plataforma.</param>
/// <param name="Tipo">Assunto, no padrão ENTIDADE_EVENTO (ex.: ATRIBUICAO_RECUSADA). O frontend escolhe ícone e tela por ele.</param>
/// <param name="Titulo">Frase curta.</param>
/// <param name="Mensagem">Detalhe.</param>
/// <param name="Link">Rota do frontend para abrir o assunto (ex.: /terceiros/atribuicoes/{id}).</param>
public sealed record NovaNotificacao(
    IReadOnlyCollection<Guid> IdsUsuarios,
    Guid? IdTenant,
    string Tipo,
    string Titulo,
    string Mensagem,
    string? Link = null);
