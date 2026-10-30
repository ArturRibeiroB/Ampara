using Funeraria.BuildingBlocks.Application.Authorization;
using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Financeiro.Application.Cobrancas;
using Funeraria.Modules.Financeiro.Domain.Cobranca;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Financeiro.Endpoints;

/// <summary>Cadastro de pagamentos da funerária (DEC-09), faturas (COB-01), baixa manual (COB-07) e webhook do Stripe (COB-06).</summary>
internal sealed class FinanceiroEndpoints : IEndpoint
{
    private const string Tag = "Financeiro";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("gestao/pagamentos/conta", async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterContaPagamentosQuery(), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .WithName("ObterContaPagamentos")
            .WithSummary("Situação do cadastro de pagamentos da funerária no Stripe")
            .Produces<ContaPagamentosResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("gestao/pagamentos/conta/cadastro", async (CadastroPagamentosRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new IniciarCadastroPagamentosCommand(r.UrlRetorno, r.UrlRenovar), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.GestaoFuneraria)
            .WithTags(Tag)
            .WithName("IniciarCadastroPagamentos")
            .WithSummary("Link da página do Stripe onde o gestor informa CNPJ, conta bancária e documentos dos sócios")
            .WithDescription("A plataforma não guarda esses dados. O link é de uso único e vale por pouco tempo: url_renovar é chamada para gerar outro. "
                + "Enquanto o cadastro não for aprovado, os contratos ficam sem cobrança e as assinaturas são criadas depois.")
            .Produces<LinkCadastroResponse>()
            .ProducesValidationProblem();

        app.MapGet("contratos/{id:guid}/faturas", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListarFaturasDoContratoQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("FaturasDoContrato")
            .WithSummary("Faturas do contrato, da mais recente para a mais antiga")
            .WithDescription("O cliente só vê as próprias. link_pagamento é a página de pagamento do Stripe; encargos_estimados é a multa e os juros se pagar hoje.")
            .Produces<IReadOnlyList<FaturaResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapGet("faturas/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterFaturaQuery(id), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.ClienteOuEquipe)
            .WithTags(Tag)
            .WithName("ObterFatura")
            .WithSummary("Detalhe da fatura")
            .Produces<FaturaResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapPost("gestao/faturas/{id:guid}/baixa", async (Guid id, BaixaManualRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new BaixaManualCommand(id, r.FormaPagamento, r.Observacao), ct)).Match(Results.Ok))
            .RequireAuthorization(Politicas.EquipeFuneraria)
            .WithTags(Tag)
            .WithName("BaixaManualFatura")
            .WithSummary("Registra um pagamento recebido fora do Stripe")
            .WithDescription("A fatura também é marcada como paga no Stripe, que para de cobrá-la. Exige a forma de pagamento e uma observação. "
                + "A baixa não lança multa e juros automaticamente: quem recebe no balcão combina o valor.")
            .Produces<FaturaResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Sem JWT: quem autentica é a assinatura do cabeçalho Stripe-Signature, conferida no caso de uso.
        app.MapPost("webhooks/stripe", async (HttpRequest request, ISender sender, CancellationToken ct) =>
            {
                using var leitor = new StreamReader(request.Body);
                string corpo = await leitor.ReadToEndAsync(ct);
                return (await sender.Send(new ReceberEventoStripeCommand(corpo, request.Headers["Stripe-Signature"].FirstOrDefault()), ct))
                    .Match(() => Results.Ok());
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}

public sealed record CadastroPagamentosRequest(string? UrlRetorno, string? UrlRenovar);

public sealed record BaixaManualRequest(FormaPagamento? FormaPagamento, string? Observacao);
