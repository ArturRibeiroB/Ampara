using System.Text.Json;
using Funeraria.BuildingBlocks.Infrastructure.Cache;
using Funeraria.BuildingBlocks.Infrastructure.Http;
using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Modules.Notificacoes.Application.Notificacoes;
using Funeraria.Modules.Notificacoes.Application.WhatsApp;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Notificacoes.Domain.Notificacoes;
using Funeraria.Modules.Notificacoes.Domain.WhatsApp;
using Funeraria.Modules.Notificacoes.Infrastructure.Email;
using Funeraria.Modules.Notificacoes.Infrastructure.Notificacoes;
using Funeraria.Modules.Notificacoes.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Funeraria.Modules.Notificacoes.Infrastructure;

public static class NotificacoesModule
{
    public static IServiceCollection AddNotificacoesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<NotificacoesDbContext>(NotificacoesDbContext.SchemaName);

        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.Section));
        services.AddScoped<RenderizadorEmail>();
        services.AddScoped<IEnvioEmail, EnvioEmail>();
        services.AddScoped<IEmailTransporte, SmtpEmailTransporte>();
        services.AddScoped<EntregaEmailJob>();

        // Notificações dentro do sistema (NOT-02). Com ConnectionStrings:Redis, o Redis é o backplane do SignalR (INF-19): uma
        // mensagem enviada numa instância chega a quem está conectado em outra. Os hubs de translado usam o mesmo SignalR.
        services.AddScoped<INotificacoesUnitOfWork>(sp => sp.GetRequiredService<NotificacoesDbContext>());
        services.AddScoped<INotificacaoRepository, NotificacaoRepository>();
        services.AddScoped<INotificacoes, ServicoNotificacoes>();
        services.AddScoped<IEntregaTempoReal, EntregaSignalR>();
        services.AddSingleton<IUserIdProvider, UsuarioDoToken>();
        ISignalRServerBuilder signalR = services.AddSignalR()
            .AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        string? redis = configuration.GetConnectionString(CacheExtensions.ConnectionStringRedis);
        if (!string.IsNullOrWhiteSpace(redis))
        {
            signalR.AddStackExchangeRedis(redis, o => o.Configuration.ChannelPrefix = RedisChannel.Literal("funeraria"));
        }

        // WhatsApp (NOT-03): desligado por padrão; só para quem deu consentimento, pela fila do Hangfire.
        services.AddOptions<WhatsAppOptions>().Bind(configuration.GetSection(WhatsAppOptions.Section));
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value.Configuracao());
        services.AddScoped<IWhatsAppRepository, WhatsAppRepository>();
        services.AddScoped<EnvioWhatsApp>();
        services.AddScoped<IEnvioWhatsApp>(sp => sp.GetRequiredService<EnvioWhatsApp>());
        services.AddScoped<EntregaWhatsApp>();
        services.AddScoped<EntregaWhatsAppJob>();
        services.AddScoped<IFilaWhatsApp, FilaWhatsAppHangfire>();
        services.AddSingleton<ILeitorWebhookWhatsApp, LeitorWebhookWhatsApp>();
        services.AddSingleton<WhatsAppFake>();
        services.AddClienteExterno<WhatsAppCloudGateway, WhatsAppCloudGateway>(configuration, _ => { });
        services.AddScoped<IGatewayWhatsApp>(sp =>
        {
            WhatsAppOptions opcoes = sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
            return string.IsNullOrWhiteSpace(opcoes.TokenAcesso) && opcoes.PermitirFake
                ? sp.GetRequiredService<WhatsAppFake>()
                : sp.GetRequiredService<WhatsAppCloudGateway>();
        });

        return services;
    }

    /// <summary>Hub em <c>/hubs/notificacoes</c>, fora do prefixo /api/v1 (NOT-02).</summary>
    public static IEndpointRouteBuilder MapNotificacoesHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<NotificacoesHub>(NotificacoesHub.Caminho);
        return app;
    }
}
