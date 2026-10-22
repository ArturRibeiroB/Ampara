using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Notificacoes.Application.Email;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Notificacoes.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Funeraria.Modules.Notificacoes.Infrastructure;

public static class NotificacoesModule
{
    public static IServiceCollection AddNotificacoesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);

        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.Section));
        services.AddScoped<RenderizadorEmail>();
        services.AddScoped<IEnvioEmail, EnvioEmail>();
        services.AddScoped<IEmailTransporte, SmtpEmailTransporte>();
        services.AddScoped<EntregaEmailJob>();

        return services;
    }
}
