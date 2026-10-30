using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.Modules.Notificacoes.Application.Notificacoes;
using Funeraria.Modules.Notificacoes.Domain.Notificacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Funeraria.Modules.Notificacoes.Infrastructure.Notificacoes;

public sealed class NotificacoesDbContext(DbContextOptions<NotificacoesDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), INotificacoesUnitOfWork
{
    public const string SchemaName = "notificacoes";

    public override string Schema => SchemaName;

    internal DbSet<Notificacao> Notificacoes => Set<Notificacao>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificacoesDbContext).Assembly);
}

internal sealed class NotificacaoConfiguration : IEntityTypeConfiguration<Notificacao>
{
    public void Configure(EntityTypeBuilder<Notificacao> builder)
    {
        builder.ToTable("notificacoes");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id_notificacao").ValueGeneratedNever();
        builder.Property(n => n.IdTenant).HasColumnName("id_tenant");
        builder.Property(n => n.Tipo).HasMaxLength(60);
        builder.Property(n => n.Titulo).HasMaxLength(200);
        builder.Property(n => n.Mensagem).HasMaxLength(1000);
        builder.Property(n => n.Link).HasMaxLength(300);

        builder.HasIndex(n => new { n.IdUsuario, n.IdTenant, n.CriadaEm });
    }
}

internal sealed class NotificacaoRepository(NotificacoesDbContext context) : INotificacaoRepository
{
    public Task<Notificacao?> ObterDoUsuarioAsync(Guid idNotificacao, Guid idUsuario, CancellationToken cancellationToken = default) =>
        context.Notificacoes.FirstOrDefaultAsync(n => n.Id == idNotificacao && n.IdUsuario == idUsuario, cancellationToken);

    public async Task<(IReadOnlyList<Notificacao> Itens, long Total, long NaoLidas)> ListarAsync(
        Guid idUsuario, Guid? idTenant, bool somenteNaoLidas, int deslocamento, int tamanho, CancellationToken cancellationToken = default)
    {
        IQueryable<Notificacao> doUsuario = DoUsuario(idUsuario, idTenant);
        IQueryable<Notificacao> consulta = somenteNaoLidas ? doUsuario.Where(n => n.LidaEm == null) : doUsuario;

        long total = await consulta.LongCountAsync(cancellationToken);
        long naoLidas = await doUsuario.LongCountAsync(n => n.LidaEm == null, cancellationToken);
        List<Notificacao> itens = await consulta
            .OrderByDescending(n => n.CriadaEm).ThenByDescending(n => n.Id)
            .Skip(deslocamento).Take(tamanho)
            .ToListAsync(cancellationToken);
        return (itens, total, naoLidas);
    }

    public async Task<IReadOnlyList<Notificacao>> NaoLidasAsync(Guid idUsuario, Guid? idTenant, CancellationToken cancellationToken = default) =>
        await DoUsuario(idUsuario, idTenant).Where(n => n.LidaEm == null).ToListAsync(cancellationToken);

    public void Adicionar(Notificacao notificacao) => context.Notificacoes.Add(notificacao);

    /// <summary>Os da funerária acessada e os da plataforma (sem funerária).</summary>
    private IQueryable<Notificacao> DoUsuario(Guid idUsuario, Guid? idTenant) =>
        context.Notificacoes.Where(n => n.IdUsuario == idUsuario && (n.IdTenant == null || n.IdTenant == idTenant));
}

/// <summary>
/// Hub de notificações (NOT-02), em <c>/hubs/notificacoes</c>. Só entrega: o cliente escuta o evento <c>notificacao</c>.
/// No navegador, o token vai em <c>?access_token=</c> (o WebSocket não manda cabeçalho de autorização).
/// </summary>
[Authorize]
public sealed class NotificacoesHub : Hub
{
    public const string Caminho = "/hubs/notificacoes";
    public const string Evento = "notificacao";
}

internal sealed class EntregaSignalR(IHubContext<NotificacoesHub> hub) : IEntregaTempoReal
{
    public Task EnviarAsync(Guid idUsuario, NotificacaoResponse notificacao, CancellationToken cancellationToken) =>
        hub.Clients.User(idUsuario.ToString()).SendAsync(NotificacoesHub.Evento, notificacao, cancellationToken);
}

/// <summary>O SignalR identifica o usuário pela claim <c>sub</c> do token, a mesma usada no resto da API.</summary>
internal sealed class UsuarioDoToken : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst("sub")?.Value;
}
