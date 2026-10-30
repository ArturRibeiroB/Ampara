using Funeraria.BuildingBlocks.Domain.Entities;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Notificacoes.Domain.Notificacoes;

/// <summary>
/// Aviso na central do usuário (NOT-02). Pertence à conta, não à funerária: a mesma pessoa pode ter avisos de várias
/// funerárias; a consulta mostra os da funerária acessada e os da plataforma.
/// </summary>
public sealed class Notificacao : Entity<Guid>
{
    private Notificacao(Guid id)
        : base(id)
    {
    }

    private Notificacao()
    {
    }

    public Guid IdUsuario { get; private set; }

    public Guid? IdTenant { get; private set; }

    public string Tipo { get; private set; } = null!;

    public string Titulo { get; private set; } = null!;

    public string Mensagem { get; private set; } = null!;

    public string? Link { get; private set; }

    public DateTime CriadaEm { get; private set; }

    public DateTime? LidaEm { get; private set; }

    public static Notificacao Criar(Guid idUsuario, Guid? idTenant, string tipo, string titulo, string mensagem, string? link, DateTime agora) =>
        new(Guid.CreateVersion7())
        {
            IdUsuario = idUsuario,
            IdTenant = idTenant,
            Tipo = tipo,
            Titulo = titulo,
            Mensagem = mensagem,
            Link = link,
            CriadaEm = agora
        };

    public void MarcarLida(DateTime agora) => LidaEm ??= agora;
}

public static class NotificacaoErros
{
    public static readonly Error NaoEncontrada =
        Error.NaoEncontrado("Notificacao.NaoEncontrada", "Notificação não encontrada.");
}

public interface INotificacaoRepository
{
    /// <summary>Notificação do usuário; a de outro usuário não aparece.</summary>
    Task<Notificacao?> ObterDoUsuarioAsync(Guid idNotificacao, Guid idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Avisos do usuário na funerária acessada e da plataforma, dos mais recentes para os mais antigos.</summary>
    Task<(IReadOnlyList<Notificacao> Itens, long Total, long NaoLidas)> ListarAsync(
        Guid idUsuario, Guid? idTenant, bool somenteNaoLidas, int deslocamento, int tamanho, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notificacao>> NaoLidasAsync(Guid idUsuario, Guid? idTenant, CancellationToken cancellationToken = default);

    void Adicionar(Notificacao notificacao);
}
