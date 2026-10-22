using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Application.Abstractions;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using Microsoft.Extensions.Logging;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Application.Commands.Logo;

/// <summary>O gestor envia o logo da funerária (TEN-03): PNG, JPEG ou WEBP de até 2 MB.</summary>
public sealed record EnviarLogoCommand(ArquivoEnviado? Arquivo) : ICommand<LogoResponse>;

/// <summary>O gestor remove o logo; e-mails e telas voltam a mostrar só o nome.</summary>
public sealed record RemoverLogoCommand : ICommand;

public sealed record LogoResponse(string LogoUrl);

internal sealed class EnviarLogoCommandValidator : AbstractValidator<EnviarLogoCommand>
{
    public EnviarLogoCommandValidator()
    {
        RuleFor(c => c.Arquivo).NotNull().WithName(ValidadorArquivo.Campo).WithMessage("Envie o arquivo do logo.");
    }
}

public sealed class EnviarLogoCommandHandler(
    IFunerariaRepository funerarias,
    ITenantsUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IArmazenamentoArquivos armazenamento,
    ILinksTenants links,
    ILogger<EnviarLogoCommandHandler> logger)
    : ICommandHandler<EnviarLogoCommand, LogoResponse>
{
    private const string Pasta = "logo";

    public async ValueTask<Result<LogoResponse>> Handle(EnviarLogoCommand command, CancellationToken cancellationToken)
    {
        FunerariaEntity? funeraria = await funerarias.ObterPorIdAsync(tenantContext.RequiredTenantId, cancellationToken);
        if (funeraria is null)
        {
            return FunerariaErros.NaoEncontrada;
        }

        Result<ArquivoSalvo> salvo = await armazenamento.SalvarAsync(funeraria.Id, Pasta, command.Arquivo!, PoliticaArquivo.Logo, cancellationToken);
        if (salvo.IsFailure)
        {
            return Result.Failure<LogoResponse>(salvo.Error);
        }

        string url = links.UrlLogo(funeraria.Slug, salvo.Value.Hash[..12]);
        string? anterior = funeraria.DefinirLogo(salvo.Value.Chave, url);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await RemoverArquivoAsync(armazenamento, anterior, logger, cancellationToken);
        return new LogoResponse(url);
    }

    /// <summary>O logo antigo sai do armazenamento depois de gravado o novo. Se falhar, fica só um arquivo órfão.</summary>
    internal static async Task RemoverArquivoAsync(IArmazenamentoArquivos armazenamento, string? chave, ILogger logger, CancellationToken cancellationToken)
    {
        if (chave is null)
        {
            return;
        }

        try
        {
            await armazenamento.RemoverAsync(chave, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Não foi possível remover o logo antigo {Chave} do armazenamento.", chave);
        }
    }
}

public sealed class RemoverLogoCommandHandler(
    IFunerariaRepository funerarias,
    ITenantsUnitOfWork unitOfWork,
    ITenantContext tenantContext,
    IArmazenamentoArquivos armazenamento,
    ILogger<RemoverLogoCommandHandler> logger)
    : ICommandHandler<RemoverLogoCommand>
{
    public async ValueTask<Result> Handle(RemoverLogoCommand command, CancellationToken cancellationToken)
    {
        FunerariaEntity? funeraria = await funerarias.ObterPorIdAsync(tenantContext.RequiredTenantId, cancellationToken);
        if (funeraria is null)
        {
            return FunerariaErros.NaoEncontrada;
        }

        string? anterior = funeraria.RemoverLogo();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await EnviarLogoCommandHandler.RemoverArquivoAsync(armazenamento, anterior, logger, cancellationToken);
        return Result.Success();
    }
}
