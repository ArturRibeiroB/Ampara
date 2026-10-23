using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Catalogo.Application.Abstractions;
using Funeraria.Modules.Catalogo.Domain.Itens;
using Funeraria.Modules.Tenants.Contracts.IntegrationEvents;

namespace Funeraria.Modules.Catalogo.Application.EventHandlers;

/// <summary>
/// Funerária nova recebe o catálogo modelo (FUN-01, INF-16, TEN-02). As categorias entram ativas e os itens
/// <b>inativos</b>: os preços são ponto de partida e a funerária revisa cada item antes de mostrar aos clientes,
/// como nos planos modelo. Idempotente: se já houver categorias, não faz nada.
/// </summary>
internal sealed class CopiarCatalogoModeloIntegrationEventHandler(
    ICategoriaRepository categorias,
    IItemCatalogoRepository itens,
    ICatalogoUnitOfWork unitOfWork,
    ITenantContext tenantContext)
    : IIntegrationEventHandler<FunerariaCriadaIntegrationEvent>
{
    public async Task Handle(FunerariaCriadaIntegrationEvent evento, CancellationToken cancellationToken)
    {
        // O barramento entrega o evento com o tenant da funerária nova; o filtro global usa esse tenant.
        if (tenantContext.TenantId != evento.IdTenant || await categorias.ExisteAlgumaAsync(cancellationToken))
        {
            return;
        }

        int ordem = 0;
        foreach ((string nomeCategoria, ItemModelo[] modelos) in CatalogoModelo.Categorias)
        {
            var categoria = Categoria.Criar(evento.IdTenant, nomeCategoria, ordem++);
            categorias.Adicionar(categoria);

            foreach (ItemModelo modelo in modelos)
            {
                var item = ItemCatalogo.Criar(
                    evento.IdTenant, categoria.Id, new DadosItem(modelo.Nome, modelo.Descricao, modelo.Preco, false, null), ativo: false);
                foreach ((string nome, decimal acrescimo) in modelo.Variacoes)
                {
                    item.AdicionarVariacao(nome, acrescimo);
                }

                itens.Adicionar(item);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal sealed record ItemModelo(string Nome, string Descricao, decimal Preco, params (string Nome, decimal Acrescimo)[] Variacoes);

/// <summary>Catálogo modelo copiado para cada funerária nova (FUN-01). Os preços são ponto de partida, não sugestão.</summary>
internal static class CatalogoModelo
{
    private const string Aviso = " Item modelo: revise o preço antes de ativar.";

    public static readonly IReadOnlyList<(string Categoria, ItemModelo[] Itens)> Categorias =
    [
        ("Urna", [new("Urna padrão", "Urna sextavada em madeira." + Aviso, 1500m, ("Pinus", -300m), ("Mogno", 1200m))]),
        ("Flores", [new("Coroa de flores", "Coroa com faixa personalizada." + Aviso, 250m, ("Média", 0m), ("Grande", 150m))]),
        ("Velório", [new("Sala de velório (24 horas)", "Sala climatizada com café." + Aviso, 800m, ("Sala VIP", 400m))]),
        ("Cerimônia", [new("Cerimônia de despedida", "Celebrante e música ambiente." + Aviso, 600m)]),
        ("Tanatopraxia", [new("Tanatopraxia", "Preparação e conservação do corpo." + Aviso, 900m)]),
        ("Sepultamento", [new("Sepultamento", "Taxas e acompanhamento no cemitério." + Aviso, 700m)]),
        ("Cremação", [new("Cremação", "Cremação com entrega das cinzas em urna simples." + Aviso, 3500m)]),
        ("Transmissão online", [new("Transmissão do velório", "Transmissão ao vivo para familiares distantes." + Aviso, 350m)]),
        ("Alimentação", [new("Coffee break", "Café, água e salgados para até 50 pessoas." + Aviso, 450m)]),
        ("Outros", [new("Livro de presença", "Livro de condolências personalizado." + Aviso, 120m)])
    ];
}
