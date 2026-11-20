using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Financeiro.Application.Plataforma;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Financeiro.Endpoints;

/// <summary>Cobrança das funerárias pela plataforma (DEC-09): mensalidade e taxa por pagamento.</summary>
internal sealed class PlataformaEndpoints : IEndpoint
{
    private const string Tag = "Cobrança da plataforma";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("gestao/pagamentos/mensalidade", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterMensalidadeQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .WithName("ObterMensalidadePlataforma")
            .WithSummary("Mensalidade que a funerária paga à plataforma, com a última fatura")
            .WithDescription("Começa quando a conta de pagamentos é liberada pelo Stripe. link_pagamento é a página do Stripe enquanto a fatura está aberta.")
            .Produces<MensalidadeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("admin/plataforma/cobranca", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterCobrancaPlataformaQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.Plataforma)
            .WithTags(Tag)
            .WithName("ObterCobrancaPlataforma")
            .WithSummary("Mensalidade e taxa padrão cobradas das funerárias")
            .Produces<CobrancaPlataformaResponse>();

        app.MapPut("admin/plataforma/cobranca", async (CobrancaPlataformaRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AlterarCobrancaPlataformaCommand(r.ValorMensalidade, r.TaxaPercentual), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.Plataforma)
            .WithTags(Tag)
            .WithName("AlterarCobrancaPlataforma")
            .WithSummary("Altera a mensalidade e a taxa padrão")
            .WithDescription("A mensalidade nova vale a partir da próxima fatura de cada funerária (aplicada pela rotina diária). "
                + "A taxa vale para as assinaturas e faturas de clientes criadas daqui em diante. Fica na auditoria.")
            .Produces<CobrancaPlataformaResponse>()
            .ProducesValidationProblem();

        app.MapPut("admin/tenants/{id:guid}/taxa-plataforma", async (Guid id, TaxaFunerariaRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new DefinirTaxaDaFunerariaCommand(id, r.TaxaPercentual), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.Plataforma)
            .WithTags(Tag)
            .WithName("DefinirTaxaDaFuneraria")
            .WithSummary("Taxa da plataforma só para esta funerária")
            .WithDescription("taxa_percentual nula volta para a taxa padrão. Vale para as assinaturas e faturas criadas daqui em diante.")
            .Produces<TaxaFunerariaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("admin/plataforma/mensalidades", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarMensalidadesQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.Plataforma)
            .WithTags(Tag)
            .WithName("ListarMensalidadesPlataforma")
            .WithSummary("Mensalidade de cada funerária, com a situação da última fatura")
            .Produces<IReadOnlyList<MensalidadeFunerariaResponse>>();
    }
}

public sealed record CobrancaPlataformaRequest(decimal? ValorMensalidade, decimal? TaxaPercentual);

public sealed record TaxaFunerariaRequest(decimal? TaxaPercentual);
