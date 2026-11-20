using Funeraria.BuildingBlocks.Infrastructure.Endpoints;
using Funeraria.Modules.Notificacoes.Application.WhatsApp;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Funeraria.Modules.Notificacoes.Endpoints;

/// <summary>
/// WhatsApp (NOT-03): consentimento do usuário logado na funerária acessada e webhook de status de entrega do provedor.
/// </summary>
internal sealed class WhatsAppEndpoints : IEndpoint
{
    private const string Tag = "Notificações";

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder grupo = app.MapGroup("notificacoes/whatsapp")
            .RequireAuthorization()
            .RequireTenant()
            .WithTags(Tag)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        grupo.MapGet(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new ObterWhatsAppQuery(), ct)).Match(Results.Ok))
            .WithName("ObterConsentimentoWhatsApp")
            .WithSummary("Se o usuário aceitou receber avisos desta funerária pelo WhatsApp")
            .Produces<WhatsAppResponse>();

        grupo.MapPut(string.Empty, async (ConsentimentoWhatsAppRequest r, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AceitarWhatsAppCommand(r.Telefone, r.Aceite), ct)).Match(Results.Ok))
            .WithName("AceitarWhatsApp")
            .WithSummary("Aceita receber avisos pelo WhatsApp (opt-in)")
            .WithDescription("Exige aceite = true. Grava o número, a data e o IP do aceite (LGPD). Chamar de novo troca o número.")
            .Produces<WhatsAppResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        grupo.MapDelete(string.Empty, async (ISender sender, CancellationToken ct) =>
                (await sender.Send(new RevogarWhatsAppCommand(), ct)).Match(Results.NoContent))
            .WithName("RevogarWhatsApp")
            .WithSummary("Para de receber avisos pelo WhatsApp")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Sem JWT: a Meta confirma o endereço com o token de verificação e assina os eventos com o segredo do app.
        app.MapGet("webhooks/whatsapp", (
                ILeitorWebhookWhatsApp leitor,
                [FromQuery(Name = "hub.mode")] string? modo,
                [FromQuery(Name = "hub.verify_token")] string? token,
                [FromQuery(Name = "hub.challenge")] string? desafio) =>
                leitor.Verificar(modo, token, desafio) is string resposta ? Results.Text(resposta) : Results.StatusCode(StatusCodes.Status403Forbidden))
            .AllowAnonymous()
            .ExcludeFromDescription();

        app.MapPost("webhooks/whatsapp", async (HttpRequest request, ISender sender, CancellationToken ct) =>
            {
                using var leitor = new StreamReader(request.Body);
                string corpo = await leitor.ReadToEndAsync(ct);
                return (await sender.Send(new ReceberStatusWhatsAppCommand(corpo, request.Headers["X-Hub-Signature-256"].FirstOrDefault()), ct))
                    .Match(() => Results.Ok());
            })
            .AllowAnonymous()
            .ExcludeFromDescription();
    }
}

public sealed record ConsentimentoWhatsAppRequest(string? Telefone, bool? Aceite);
