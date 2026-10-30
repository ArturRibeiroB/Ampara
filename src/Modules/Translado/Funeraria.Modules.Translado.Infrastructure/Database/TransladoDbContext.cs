using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Application.Serialization;
using Funeraria.BuildingBlocks.Domain.Estados;
using Funeraria.BuildingBlocks.Infrastructure.Database;
using Funeraria.BuildingBlocks.Infrastructure.Tenancy;
using Funeraria.Modules.Translado.Application.Translados;
using Funeraria.Modules.Translado.Domain.Translados;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransladoEntity = Funeraria.Modules.Translado.Domain.Translados.Translado;

namespace Funeraria.Modules.Translado.Infrastructure.Database;

public sealed class TransladoDbContext(DbContextOptions<TransladoDbContext> options, ITenantContext tenantContext)
    : ModuleDbContext(options, tenantContext), ITransladoUnitOfWork
{
    public const string SchemaName = "translado";

    public override string Schema => SchemaName;

    internal DbSet<TransladoEntity> Translados => Set<TransladoEntity>();

    internal DbSet<ConfiguracaoTranslado> Configuracoes => Set<ConfiguracaoTranslado>();

    internal DbSet<DistanciaCalculada> Distancias => Set<DistanciaCalculada>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TransladoDbContext).Assembly);
}

internal sealed class TransladoConfiguration : IEntityTypeConfiguration<TransladoEntity>
{
    public void Configure(EntityTypeBuilder<TransladoEntity> builder)
    {
        builder.ToTable("translados");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id_translado").ValueGeneratedNever();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.StatusAceite).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.NomeFalecido).HasMaxLength(150);
        builder.Property(t => t.ContatoNome).HasMaxLength(150);
        builder.Property(t => t.ContatoTelefone).HasMaxLength(20);
        builder.Property(t => t.DistanciaKm).HasPrecision(10, 2);
        builder.Property(t => t.Valor).HasPrecision(12, 2);
        builder.Property(t => t.ValorCobertoPlano).HasPrecision(12, 2);
        builder.Property(t => t.NomeFrota).HasMaxLength(200);
        builder.Property(t => t.MotivoRecusa).HasMaxLength(500);
        builder.Property(t => t.MotoristaNome).HasMaxLength(150);
        builder.Property(t => t.MotoristaTelefone).HasMaxLength(20);
        builder.Property(t => t.VeiculoPlaca).HasMaxLength(10);
        builder.Property(t => t.VeiculoModelo).HasMaxLength(100);
        Endereco(builder.ComplexProperty(t => t.Origem), "origem");
        Endereco(builder.ComplexProperty(t => t.Destino), "destino");

        builder.HasMany(t => t.Historico).WithOne().HasForeignKey("IdTranslado").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.Historico).UsePropertyAccessMode(PropertyAccessMode.Field).AutoInclude();

        builder.HasIndex(t => t.IdFuneral);
        builder.HasIndex(t => t.IdTerceiroFrota);
    }

    private static void Endereco(ComplexPropertyBuilder<Endereco> endereco, string prefixo)
    {
        endereco.Property(e => e.Logradouro).HasColumnName($"{prefixo}_endereco").HasMaxLength(200);
        endereco.Property(e => e.Cidade).HasColumnName($"{prefixo}_cidade").HasMaxLength(100);
        endereco.Property(e => e.Uf).HasColumnName($"{prefixo}_uf").HasMaxLength(2);
        endereco.Property(e => e.Pais).HasColumnName($"{prefixo}_pais").HasMaxLength(2);
        endereco.Ignore(e => e.Resumo);
    }
}

internal sealed class HistoricoTransladoConfiguration : IEntityTypeConfiguration<HistoricoStatus<StatusTranslado>>
{
    public void Configure(EntityTypeBuilder<HistoricoStatus<StatusTranslado>> builder)
    {
        builder.ToTable("translados_historico");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id_historico").ValueGeneratedNever();
        builder.Property<Guid>("IdTranslado").HasColumnName("id_translado");
        builder.Property(h => h.De).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Para).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Acao).HasMaxLength(40);
        builder.Property(h => h.Observacao).HasMaxLength(1000);
    }
}

internal sealed class ConfiguracaoTransladoConfiguration : IEntityTypeConfiguration<ConfiguracaoTranslado>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoTranslado> builder)
    {
        builder.ToTable("configuracoes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id_configuracao").ValueGeneratedNever();
        builder.Property(c => c.Faixas)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<List<FaixaKm>>(v, JsonbOptions.Default) ?? new List<FaixaKm>(),
                new ValueComparer<IReadOnlyList<FaixaKm>>((a, b) => a!.SequenceEqual(b!), v => v.Count, v => v.ToList()));
        builder.Property(c => c.DocumentosExigidos)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonbOptions.Default),
                v => JsonSerializer.Deserialize<Dictionary<TipoTranslado, IReadOnlyList<string>>>(v, JsonbOptions.Default) ?? new Dictionary<TipoTranslado, IReadOnlyList<string>>(),
                new ValueComparer<IReadOnlyDictionary<TipoTranslado, IReadOnlyList<string>>>(
                    (a, b) => JsonSerializer.Serialize(a, JsonbOptions.Default) == JsonSerializer.Serialize(b, JsonbOptions.Default),
                    v => v.Count,
                    v => v));
        builder.HasIndex(c => c.TenantId).IsUnique();
    }
}

internal sealed class DistanciaCalculadaConfiguration : IEntityTypeConfiguration<DistanciaCalculada>
{
    public void Configure(EntityTypeBuilder<DistanciaCalculada> builder)
    {
        builder.ToTable("distancias_calculadas");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id_distancia").ValueGeneratedNever();
        builder.Property(d => d.Chave).HasMaxLength(800);
        builder.Property(d => d.Km).HasPrecision(10, 2);
        builder.HasIndex(d => d.Chave).IsUnique();
    }
}

internal sealed class TransladoRepository(TransladoDbContext context) : ITransladoRepository
{
    public Task<TransladoEntity?> ObterAsync(Guid idTranslado, CancellationToken cancellationToken = default) =>
        context.Translados.FirstOrDefaultAsync(t => t.Id == idTranslado, cancellationToken);

    public async Task<IReadOnlyList<TransladoEntity>> ListarDoFuneralAsync(Guid idFuneral, CancellationToken cancellationToken = default) =>
        await context.Translados.Where(t => t.IdFuneral == idFuneral).OrderBy(t => t.DataPrevista).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TransladoEntity>> ListarDaFrotaAsync(Guid idTerceiro, CancellationToken cancellationToken = default) =>
        await context.Translados.AsNoTracking().Where(t => t.IdTerceiroFrota == idTerceiro).OrderBy(t => t.DataPrevista).ToListAsync(cancellationToken);

    public Task<ConfiguracaoTranslado?> ObterConfiguracaoAsync(CancellationToken cancellationToken = default) =>
        context.Configuracoes.FirstOrDefaultAsync(cancellationToken);

    public void AdicionarConfiguracao(ConfiguracaoTranslado configuracao) => context.Configuracoes.Add(configuracao);

    public async Task<decimal?> ObterDistanciaAsync(string chave, CancellationToken cancellationToken = default) =>
        await context.Distancias.Where(d => d.Chave == chave).Select(d => (decimal?)d.Km).FirstOrDefaultAsync(cancellationToken);

    public void AdicionarDistancia(DistanciaCalculada distancia) => context.Distancias.Add(distancia);

    public void Adicionar(TransladoEntity translado) => context.Translados.Add(translado);
}

/// <summary>Seção "OpenRouteService" (DEC-10). A chave fica em secrets; sem ela, o funcionário informa os km.</summary>
public sealed class OpenRouteServiceOptions
{
    public const string Section = "OpenRouteService";

    public string? ApiKey { get; set; }

    public string UrlBase { get; set; } = "https://api.openrouteservice.org/";
}

/// <summary>
/// Distância por rota no OpenStreetMap, pela API do OpenRouteService (DEC-10). Qualquer falha vira nulo: o translado nunca fica
/// travado pela API, e o funcionário informa os km.
/// </summary>
public sealed partial class OpenRouteServiceGateway(HttpClient http, IOptions<OpenRouteServiceOptions> opcoes, ILogger<OpenRouteServiceGateway> logger) : IDistanciaGateway
{
    public async Task<decimal?> CalcularKmAsync(Endereco origem, Endereco destino, CancellationToken cancellationToken)
    {
        string? chave = opcoes.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(chave))
        {
            return null;
        }

        try
        {
            double[]? de = await GeocodificarAsync(origem, chave, cancellationToken);
            double[]? para = await GeocodificarAsync(destino, chave, cancellationToken);
            if (de is null || para is null)
            {
                return null;
            }

            using var pedido = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(opcoes.Value.UrlBase), "v2/directions/driving-car"))
            {
                Content = JsonContent.Create(new { coordinates = new[] { de, para } })
            };
            pedido.Headers.TryAddWithoutValidation("Authorization", chave);
            using HttpResponseMessage resposta = await http.SendAsync(pedido, cancellationToken);
            resposta.EnsureSuccessStatusCode();

            using JsonDocument rota = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(cancellationToken));
            double metros = rota.RootElement.GetProperty("routes")[0].GetProperty("summary").GetProperty("distance").GetDouble();
            return decimal.Round((decimal)metros / 1000m, 2, MidpointRounding.AwayFromZero);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            // Sem endereços no log (LGPD): só o tipo do erro.
            LogFalha(logger, e.GetType().Name);
            return null;
        }
    }

    private async Task<double[]?> GeocodificarAsync(Endereco endereco, string chave, CancellationToken cancellationToken)
    {
        string texto = Uri.EscapeDataString($"{endereco.Logradouro}, {endereco.Cidade}, {endereco.Uf}");
        using HttpResponseMessage resposta = await http.GetAsync(
            new Uri(new Uri(opcoes.Value.UrlBase), $"geocode/search?api_key={Uri.EscapeDataString(chave)}&text={texto}&boundary.country=BR&size=1"), cancellationToken);
        resposta.EnsureSuccessStatusCode();

        using JsonDocument local = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(cancellationToken));
        JsonElement encontrados = local.RootElement.GetProperty("features");
        if (encontrados.GetArrayLength() == 0)
        {
            return null;
        }

        JsonElement coordenadas = encontrados[0].GetProperty("geometry").GetProperty("coordinates");
        return [coordenadas[0].GetDouble(), coordenadas[1].GetDouble()];
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Distância não calculada pelo OpenRouteService ({Erro}): o funcionário informa os km")]
    private static partial void LogFalha(ILogger logger, string erro);
}

/// <summary>
/// Hub do translado (TRA-06), em <c>/hubs/translados</c>. O cliente chama <c>EntrarTranslado</c> e escuta
/// <c>StatusTransladoAtualizado</c>. O grupo só aceita quem pode ver o translado: a equipe da funerária, o titular ou a empresa atribuída.
/// </summary>
[Authorize]
public sealed class TransladosHub(AcessoAoTranslado acesso, TenantContext tenantContext) : Hub
{
    public const string Caminho = "/hubs/translados";
    public const string Evento = "StatusTransladoAtualizado";

    public static string Grupo(Guid idTranslado) => $"translado-{idTranslado}";

    public async Task EntrarTranslado(Guid idTranslado)
    {
        // O hub não passa pelo middleware de tenant a cada chamada: o tenant vem do token da conexão.
        ClaimsPrincipal? usuario = Context.User;
        tenantContext.Set(Guid.TryParse(usuario?.FindFirst(Claims.IdTenant)?.Value, out Guid tenant) ? tenant : null);
        bool pode = tenantContext.TenantId is not null && await acesso.PodeAcompanharAsync(
            idTranslado,
            usuario?.FindFirst(Claims.Perfil)?.Value,
            Guid.TryParse(usuario?.FindFirst(Claims.IdCliente)?.Value, out Guid cliente) ? cliente : null,
            Guid.TryParse(usuario?.FindFirst(Claims.IdTerceiro)?.Value, out Guid terceiro) ? terceiro : null,
            Context.ConnectionAborted);
        if (!pode)
        {
            throw new HubException("Você não pode acompanhar este translado.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(idTranslado), Context.ConnectionAborted);
    }

    public Task SairTranslado(Guid idTranslado) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Grupo(idTranslado), Context.ConnectionAborted);
}

internal sealed class EntregaStatusSignalR(IHubContext<TransladosHub> hub) : IEntregaStatusTranslado
{
    public Task EnviarAsync(Guid idTranslado, StatusTranslado status, DateTime dataHora, string? observacao, CancellationToken cancellationToken) =>
        hub.Clients.Group(TransladosHub.Grupo(idTranslado)).SendAsync(
            TransladosHub.Evento,
            new { id_translado = idTranslado, status = NomeStatus(status), data_hora = dataHora, observacao },
            cancellationToken);

    private static string NomeStatus(StatusTranslado status) => BuildingBlocks.Domain.Results.NomeApi.De(status);
}
