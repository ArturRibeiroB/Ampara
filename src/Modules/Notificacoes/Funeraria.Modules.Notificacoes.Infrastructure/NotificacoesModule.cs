using System.Text.Json;
using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Modules.Notificacoes.Application.Notificacoes;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Notificacoes.Domain.Notificacoes;
using Funeraria.Modules.Notificacoes.Infrastructure.Email;
using Funeraria.Modules.Notificacoes.Infrastructure.Notificacoes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // Notificações dentro do sistema (NOT-02). Com mais de uma instância da API, ligar o backplane Redis (INF-19).
        services.AddScoped<INotificacoesUnitOfWork>(sp => sp.GetRequiredService<NotificacoesDbContext>());
        services.AddScoped<INotificacaoRepository, NotificacaoRepository>();
        services.AddScoped<INotificacoes, ServicoNotificacoes>();
        services.AddScoped<IEntregaTempoReal, EntregaSignalR>();
        services.AddSingleton<IUserIdProvider, UsuarioDoToken>();
        services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        return services;
    }

    /// <summary>Hub em <c>/hubs/notificacoes</c>, fora do prefixo /api/v1 (NOT-02).</summary>
    public static IEndpointRouteBuilder MapNotificacoesHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<NotificacoesHub>(NotificacoesHub.Caminho);
        return app;
    }
}
