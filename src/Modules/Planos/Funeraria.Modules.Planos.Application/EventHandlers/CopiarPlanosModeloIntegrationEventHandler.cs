using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Planos.Application.Abstractions;
using Funeraria.Modules.Planos.Domain.Planos;
using Funeraria.Modules.Tenants.Contracts.IntegrationEvents;

namespace Funeraria.Modules.Planos.Application.EventHandlers;

/// <summary>
/// Funerária nova recebe os planos modelo (INF-16, TEN-02). Eles entram <b>inativos</b>: os valores são sugestão
/// e a funerária revisa e ativa cada plano antes de oferecer aos clientes. Idempotente: se já houver planos, não faz nada.
/// </summary>
internal sealed class CopiarPlanosModeloIntegrationEventHandler(
    IPlanoRepository planos,
    IPlanosUnitOfWork unitOfWork,
    ITenantContext tenantContext)
    : IIntegrationEventHandler<FunerariaCriadaIntegrationEvent>
{
    public async Task Handle(FunerariaCriadaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        // O barramento já entrega o evento com o tenant da funerária nova; o filtro global usa esse tenant.
        if (tenantContext.TenantId != evento.IdTenant || await planos.ExisteAlgumAsync(cancellationToken))
        {
            return;
        }

        foreach (PlanoModelo modelo in PlanoModelo.Todos)
        {
            var plano = Plano.Criar(
                evento.IdTenant,
                modelo.Nome,
                modelo.Descricao,
                modelo.Precos,
                modelo.CarenciaDias,
                modelo.LimiteDependentes,
                permiteSomenteDependente: false,
                modelo.Configuracoes);

            plano.AlterarStatus(false);
            planos.Adicionar(plano);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Planos modelo copiados para cada funerária nova. Os valores são ponto de partida, não preço sugerido.</summary>
internal sealed record PlanoModelo(
    string Nome,
    string Descricao,
    PrecosPlano Precos,
    int CarenciaDias,
    int LimiteDependentes,
    PlanoConfiguracoes Configuracoes)
{
    private const string Aviso = " Plano modelo: revise valores e coberturas antes de ativar.";

    public static readonly IReadOnlyList<PlanoModelo> Todos =
    [
        new("Essencial", "Cobertura básica para o titular." + Aviso,
            new PrecosPlano(49.90m, 538.92m, 9.90m, 106.92m), 90, 2,
            new PlanoConfiguracoes
            {
                Translado = new ConfiguracaoTranslado { KmIncluso = 50 },
                Velorio = new ConfiguracaoVelorio { HorasIncluidas = 12 }
            }),
        new("Familiar", "Titular e família, com velório de 24 horas." + Aviso,
            new PrecosPlano(79.90m, 862.92m, 14.90m, 160.92m), 90, 6,
            new PlanoConfiguracoes
            {
                Translado = new ConfiguracaoTranslado { KmIncluso = 100 },
                Velorio = new ConfiguracaoVelorio { HorasIncluidas = 24 },
                Dependentes = new ConfiguracaoDependentes { IdadeMaximaFilhos = 21 }
            }),
        new("Premium", "Cobertura completa, com cremação e transmissão online." + Aviso,
            new PrecosPlano(129.90m, 1402.92m, 19.90m, 214.92m), 60, 8,
            new PlanoConfiguracoes
            {
                Translado = new ConfiguracaoTranslado { KmIncluso = 300 },
                Velorio = new ConfiguracaoVelorio { HorasIncluidas = 24, SalaVip = true },
                CremacaoInclusa = true,
                TransmissaoOnline = true,
                Dependentes = new ConfiguracaoDependentes { IdadeMaximaFilhos = 24 }
            })
    ];
}
