using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Notificacoes.Application.Notificacoes;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Notificacoes.Domain.WhatsApp;

namespace Funeraria.Modules.Notificacoes.Application.WhatsApp;

/// <summary>Provedor do WhatsApp (NOT-03): a API oficial da Meta (WhatsApp Cloud API) ou uma fake nos testes.</summary>
public interface IGatewayWhatsApp
{
    Task<ResultadoEnvioWhatsApp> EnviarTemplateAsync(
        string telefone, string template, string idioma, IReadOnlyList<string> parametros, CancellationToken cancellationToken);
}

/// <param name="IdExterno">Id da mensagem no provedor, quando aceita.</param>
/// <param name="Erro">Motivo da recusa.</param>
/// <param name="Definitivo">Recusa que não adianta tentar de novo (número inválido, template inexistente).</param>
public sealed record ResultadoEnvioWhatsApp(string? IdExterno, string? Erro = null, bool Definitivo = false)
{
    public bool Aceito => IdExterno is not null;
}

/// <summary>Coloca a mensagem na fila de envio (Hangfire, com retry).</summary>
public interface IFilaWhatsApp
{
    void Enfileirar(Guid idMensagem);
}

/// <summary>Configuração lida pela Infrastructure (seção <c>WhatsApp</c>).</summary>
/// <param name="Habilitado">Canal ligado.</param>
/// <param name="Idioma">Idioma dos templates (ex.: pt_BR).</param>
/// <param name="Templates">Modelo interno → nome do template aprovado.</param>
/// <param name="TiposNotificacao">Tipos da central que também vão pelo WhatsApp (ex.: FUNERAL_STATUS).</param>
public sealed record ConfiguracaoWhatsApp(
    bool Habilitado,
    string Idioma,
    IReadOnlyDictionary<string, string> Templates,
    IReadOnlySet<string> TiposNotificacao);

/// <summary>Retorno de status lido do webhook do provedor.</summary>
public sealed record StatusWhatsAppRecebido(string IdExterno, StatusMensagemWhatsApp Status, string? Erro);

/// <summary>Valida a assinatura do webhook e lê os status de entrega.</summary>
public interface ILeitorWebhookWhatsApp
{
    /// <summary>Nulo quando a assinatura não confere.</summary>
    IReadOnlyList<StatusWhatsAppRecebido>? Ler(string corpo, string? assinatura);

    /// <summary>Confirmação do endereço pelo provedor: devolve o desafio quando o token de verificação confere.</summary>
    string? Verificar(string? modo, string? token, string? desafio);
}

public static class TelefoneWhatsApp
{
    /// <summary>"(44) 99999-0000" → "5544999990000". Nulo se não for um número brasileiro com DDD.</summary>
    public static string? Normalizar(string? telefone)
    {
        string digitos = DocumentosBrasileiros.Normalizar(telefone ?? string.Empty);
        if (!digitos.All(char.IsAsciiDigit))
        {
            return null;
        }

        return digitos.Length switch
        {
            10 or 11 => "55" + digitos,
            12 or 13 when digitos.StartsWith("55", StringComparison.Ordinal) => digitos,
            _ => null
        };
    }
}

/// <summary>Grava a mensagem e enfileira, só com consentimento ativo e template configurado.</summary>
public sealed class EnvioWhatsApp(
    IWhatsAppRepository repositorio,
    INotificacoesUnitOfWork unitOfWork,
    IFilaWhatsApp fila,
    ConfiguracaoWhatsApp configuracao,
    TimeProvider timeProvider)
    : IEnvioWhatsApp
{
    public async Task EnfileirarAsync(MensagemWhatsAppSolicitada mensagem, CancellationToken cancellationToken = default)
    {
        if (!configuracao.Habilitado || !configuracao.Templates.ContainsKey(mensagem.Modelo))
        {
            return;
        }

        if (await repositorio.ObterConsentimentoAsync(mensagem.IdTenant, mensagem.IdUsuario, cancellationToken) is not { Ativo: true } consentimento)
        {
            return;
        }

        var nova = MensagemWhatsApp.Criar(
            mensagem.IdTenant, mensagem.IdUsuario, mensagem.Modelo, consentimento.Telefone, mensagem.Parametros, timeProvider.GetUtcNow().UtcDateTime);
        repositorio.AdicionarMensagem(nova);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        fila.Enfileirar(nova.Id);
    }

    /// <summary>Aviso da central: vai também pelo WhatsApp quando o tipo está em <c>WhatsApp:TiposNotificacao</c>.</summary>
    public Task DaNotificacaoAsync(Guid idUsuario, Guid idTenant, string tipo, string titulo, string texto, CancellationToken cancellationToken) =>
        configuracao.TiposNotificacao.Contains(tipo)
            ? EnfileirarAsync(new MensagemWhatsAppSolicitada(idUsuario, idTenant, ModelosWhatsApp.Notificacao, [titulo, texto]), cancellationToken)
            : Task.CompletedTask;
}

/// <summary>Envia uma mensagem da fila. Falha temporária vira exceção, e o Hangfire tenta de novo.</summary>
public sealed class EntregaWhatsApp(
    IWhatsAppRepository repositorio,
    INotificacoesUnitOfWork unitOfWork,
    IGatewayWhatsApp gateway,
    ConfiguracaoWhatsApp configuracao,
    TimeProvider timeProvider)
{
    public async Task EntregarAsync(Guid idMensagem, CancellationToken cancellationToken)
    {
        MensagemWhatsApp? mensagem = await repositorio.ObterMensagemAsync(idMensagem, cancellationToken);
        if (mensagem is not { Status: StatusMensagemWhatsApp.Pendente })
        {
            return;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;

        // O consentimento pode ter sido revogado entre o pedido e o envio.
        if (await repositorio.ObterConsentimentoAsync(mensagem.TenantId, mensagem.IdUsuario, cancellationToken) is not { Ativo: true }
            || !configuracao.Templates.TryGetValue(mensagem.Modelo, out string? template))
        {
            mensagem.AtualizarStatus(StatusMensagemWhatsApp.Falhou, "Sem consentimento ou template no momento do envio.", agora);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        ResultadoEnvioWhatsApp resultado = await gateway.EnviarTemplateAsync(mensagem.Telefone, template, configuracao.Idioma, mensagem.Parametros, cancellationToken);
        if (resultado.Aceito)
        {
            mensagem.Enviada(resultado.IdExterno!, agora);
        }
        else if (resultado.Definitivo)
        {
            mensagem.AtualizarStatus(StatusMensagemWhatsApp.Falhou, resultado.Erro, agora);
        }
        else
        {
            throw new InvalidOperationException($"WhatsApp recusou a mensagem {idMensagem} temporariamente: {resultado.Erro}");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>Situação do consentimento do usuário logado na funerária acessada (NOT-03).</summary>
public sealed record ObterWhatsAppQuery : IQuery<WhatsAppResponse>;

/// <summary>Aceite (opt-in): grava o número, a data e o IP. O texto do aceite é mostrado pelo frontend.</summary>
public sealed record AceitarWhatsAppCommand(string? Telefone, bool? Aceite) : ICommand<WhatsAppResponse>;

/// <summary>Revoga o consentimento: nada mais é enviado.</summary>
public sealed record RevogarWhatsAppCommand : ICommand;

/// <param name="Disponivel">O canal está ligado na plataforma.</param>
/// <param name="Ativo">O usuário aceitou e não revogou.</param>
/// <param name="Telefone">Número cadastrado, mascarado.</param>
/// <param name="AceitoEm">Quando aceitou.</param>
public sealed record WhatsAppResponse(bool Disponivel, bool Ativo, string? Telefone, DateTime? AceitoEm);

internal sealed class AceitarWhatsAppCommandValidator : AbstractValidator<AceitarWhatsAppCommand>
{
    public AceitarWhatsAppCommandValidator()
    {
        RuleFor(c => c.Telefone).Must(t => TelefoneWhatsApp.Normalizar(t) is not null).WithMessage(WhatsAppErros.TelefoneInvalido.Description);
        RuleFor(c => c.Aceite).Equal(true).WithMessage(WhatsAppErros.AceiteObrigatorio.Description);
    }
}

internal static class RespostaWhatsApp
{
    public static WhatsAppResponse De(ConsentimentoWhatsApp? c, bool disponivel) =>
        c is { Ativo: true }
            ? new WhatsAppResponse(disponivel, true, Mascarar(c.Telefone), c.AceitoEm)
            : new WhatsAppResponse(disponivel, false, null, null);

    /// <summary>"5544999990000" → "(44) *****-0000".</summary>
    private static string Mascarar(string telefone) => telefone.Length >= 12 ? $"({telefone[2..4]}) *****-{telefone[^4..]}" : "***";
}

public sealed class ObterWhatsAppQueryHandler(IWhatsAppRepository repositorio, ICurrentUser usuario, ITenantContext tenantContext, ConfiguracaoWhatsApp configuracao)
    : IQueryHandler<ObterWhatsAppQuery, WhatsAppResponse>
{
    public async ValueTask<Result<WhatsAppResponse>> Handle(ObterWhatsAppQuery query, CancellationToken cancellationToken) =>
        RespostaWhatsApp.De(
            await repositorio.ObterConsentimentoAsync(tenantContext.RequiredTenantId, usuario.IdUsuario!.Value, cancellationToken),
            configuracao.Habilitado);
}

public sealed class AceitarWhatsAppCommandHandler(
    IWhatsAppRepository repositorio,
    INotificacoesUnitOfWork unitOfWork,
    ICurrentUser usuario,
    ITenantContext tenantContext,
    IOrigemRequisicao origem,
    ConfiguracaoWhatsApp configuracao,
    TimeProvider timeProvider)
    : ICommandHandler<AceitarWhatsAppCommand, WhatsAppResponse>
{
    public async ValueTask<Result<WhatsAppResponse>> Handle(AceitarWhatsAppCommand command, CancellationToken cancellationToken)
    {
        if (!configuracao.Habilitado)
        {
            return Result.Failure<WhatsAppResponse>(WhatsAppErros.Desligado);
        }

        Guid idTenant = tenantContext.RequiredTenantId;
        Guid idUsuario = usuario.IdUsuario!.Value;
        string telefone = TelefoneWhatsApp.Normalizar(command.Telefone)!;
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;

        ConsentimentoWhatsApp? consentimento = await repositorio.ObterConsentimentoAsync(idTenant, idUsuario, cancellationToken);
        if (consentimento is null)
        {
            consentimento = ConsentimentoWhatsApp.Aceitar(idTenant, idUsuario, telefone, agora, origem.Ip);
            repositorio.AdicionarConsentimento(consentimento);
        }
        else
        {
            consentimento.AceitarDeNovo(telefone, agora, origem.Ip);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RespostaWhatsApp.De(consentimento, configuracao.Habilitado);
    }
}

public sealed class RevogarWhatsAppCommandHandler(
    IWhatsAppRepository repositorio,
    INotificacoesUnitOfWork unitOfWork,
    ICurrentUser usuario,
    ITenantContext tenantContext,
    TimeProvider timeProvider)
    : ICommandHandler<RevogarWhatsAppCommand>
{
    public async ValueTask<Result> Handle(RevogarWhatsAppCommand command, CancellationToken cancellationToken)
    {
        ConsentimentoWhatsApp? consentimento = await repositorio.ObterConsentimentoAsync(
            tenantContext.RequiredTenantId, usuario.IdUsuario!.Value, cancellationToken);
        if (consentimento is null)
        {
            return WhatsAppErros.SemConsentimento;
        }

        consentimento.Revogar(timeProvider.GetUtcNow().UtcDateTime);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}

/// <summary>Webhook de status do provedor (enviada, entregue, lida, falhou). Mensagem desconhecida é ignorada.</summary>
public sealed record ReceberStatusWhatsAppCommand(string Corpo, string? Assinatura) : ICommand;

public sealed class ReceberStatusWhatsAppCommandHandler(
    ILeitorWebhookWhatsApp leitor,
    IWhatsAppRepository repositorio,
    INotificacoesUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<ReceberStatusWhatsAppCommand>
{
    private static readonly Error AssinaturaInvalida =
        Error.Validacao("WhatsApp.AssinaturaInvalida", "Assinatura do webhook do WhatsApp inválida.");

    public async ValueTask<Result> Handle(ReceberStatusWhatsAppCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<StatusWhatsAppRecebido>? recebidos = leitor.Ler(command.Corpo, command.Assinatura);
        if (recebidos is null)
        {
            return AssinaturaInvalida;
        }

        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        foreach (StatusWhatsAppRecebido recebido in recebidos)
        {
            if (await repositorio.ObterPorIdExternoAsync(recebido.IdExterno, cancellationToken) is MensagemWhatsApp mensagem)
            {
                mensagem.AtualizarStatus(recebido.Status, recebido.Erro, agora);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
