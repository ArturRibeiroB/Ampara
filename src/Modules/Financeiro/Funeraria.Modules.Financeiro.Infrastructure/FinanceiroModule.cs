using Funeraria.BuildingBlocks.Infrastructure.Jobs;
using Funeraria.BuildingBlocks.Infrastructure.Modules;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Application.Cobrancas;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Funeraria.Modules.Financeiro.Infrastructure.Database;
using Funeraria.Modules.Financeiro.Infrastructure.Stripe;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stripe;

namespace Funeraria.Modules.Financeiro.Infrastructure;

public static class FinanceiroModule
{
    public static IServiceCollection AddFinanceiroModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddModuleApplication(Application.AssemblyReference.Assembly);
        services.AddModuleDbContext<FinanceiroDbContext>(FinanceiroDbContext.SchemaName);
        services.AddOptions<StripeOptions>().Bind(configuration.GetSection(StripeOptions.Section));

        services.AddScoped<IFinanceiroUnitOfWork>(sp => sp.GetRequiredService<FinanceiroDbContext>());
        services.AddScoped<IContaPagamentoRepository, ContaPagamentoRepository>();
        services.AddScoped<IAssinaturaRepository, AssinaturaRepository>();
        services.AddScoped<IFaturaRepository, FaturaRepository>();
        services.AddScoped<IEventoStripeRepository, EventoStripeRepository>();

        services.AddScoped<EspelhoDeFaturas>();
        services.AddScoped<ProvisionamentoAssinaturas>();
        services.AddScoped<ApresentacaoDeFaturas>();
        services.AddScoped<AvisosDeCobranca>();
        services.AddScoped<ProcessamentoEventosStripe>();
        services.AddScoped<RotinasDeCobranca>();
        services.AddScoped<ProcessarEventoStripeJob>();
        services.AddScoped<IJobRecorrente, RotinaDiariaCobrancaJob>();
        services.AddScoped<IFilaEventosStripe, FilaEventosHangfire>();
        services.AddSingleton<ILeitorWebhook, LeitorWebhookStripe>();

        // Stripe com chave; sem chave, a fake só onde a configuração permite. A falta da chave em produção aparece na primeira
        // cobrança, com a mensagem abaixo, e não derruba o resto da API.
        services.AddSingleton<GatewayFake>();
        services.AddSingleton<IStripeClient>(sp => new StripeClient(
            sp.GetRequiredService<IOptions<StripeOptions>>().Value.SecretKey
            ?? throw new InvalidOperationException("Configure Stripe:SecretKey (secret) ou, em desenvolvimento, Stripe:PermitirFake.")));
        services.AddScoped<IPagamentoGateway>(sp =>
        {
            StripeOptions opcoes = sp.GetRequiredService<IOptions<StripeOptions>>().Value;
            return string.IsNullOrWhiteSpace(opcoes.SecretKey) && opcoes.PermitirFake
                ? sp.GetRequiredService<GatewayFake>()
                : new StripeGateway(sp.GetRequiredService<IStripeClient>(), sp.GetRequiredService<IOptions<StripeOptions>>());
        });

        return services;
    }
}
