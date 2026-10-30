using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Financeiro.Application.Abstractions;
using Funeraria.Modules.Financeiro.Domain.Cobranca;

namespace Funeraria.Modules.Financeiro.Application.Cobrancas;

/// <summary>Coloca o evento na fila de processamento em segundo plano (Hangfire).</summary>
public interface IFilaEventosStripe
{
    void Enfileirar(Guid idEvento);
}

/// <summary>
/// Evento vindo do Stripe (COB-06): confere a assinatura, grava e enfileira, para responder 200 na hora. O id do evento é
/// único: uma reentrega do mesmo evento só responde 200.
/// </summary>
public sealed record ReceberEventoStripeCommand(string Corpo, string? Assinatura) : ICommand;

public sealed class ReceberEventoStripeCommandHandler(
    ILeitorWebhook leitor,
    IEventoStripeRepository eventos,
    IFinanceiroUnitOfWork unitOfWork,
    IFilaEventosStripe fila,
    TimeProvider timeProvider)
    : ICommandHandler<ReceberEventoStripeCommand>
{
    private static readonly Error AssinaturaInvalida =
        Error.Validacao("Webhook.AssinaturaInvalida", "Assinatura do Stripe inválida.");

    public async ValueTask<Result> Handle(ReceberEventoStripeCommand command, CancellationToken cancellationToken)
    {
        EventoGateway? evento = command.Assinatura is null ? null : leitor.Validar(command.Corpo, command.Assinatura);
        if (evento is null)
        {
            return AssinaturaInvalida;
        }

        var registro = EventoStripe.Receber(evento.Id, evento.Tipo, evento.IdConta, command.Corpo, timeProvider.GetUtcNow().UtcDateTime);
        eventos.Adicionar(registro);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            return Result.Success();
        }

        fila.Enfileirar(registro.Id);
        return Result.Success();
    }
}
