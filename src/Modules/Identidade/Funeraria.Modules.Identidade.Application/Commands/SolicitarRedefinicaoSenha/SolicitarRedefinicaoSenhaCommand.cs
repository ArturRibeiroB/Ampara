using System.Globalization;
using FluentValidation;
using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Application.Validation;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Identidade.Application.Commands.SolicitarRedefinicaoSenha;

/// <summary>
/// "Esqueci minha senha" (AUT-05). Responde sucesso sempre, exista o e-mail ou não, para não revelar quem tem conta.
/// O link só é enviado para quem pode entrar no endereço acessado: na funerária, quem tem vínculo ativo com ela;
/// no endereço da plataforma, o ADMIN.
/// </summary>
public sealed record SolicitarRedefinicaoSenhaCommand(string Email) : ICommand;

internal sealed class SolicitarRedefinicaoSenhaCommandValidator : AbstractValidator<SolicitarRedefinicaoSenhaCommand>
{
    public SolicitarRedefinicaoSenhaCommandValidator()
    {
        RuleFor(c => c.Email).EmailObrigatorio();
    }
}

public sealed class SolicitarRedefinicaoSenhaCommandHandler(
    IUsuarios usuarios,
    IVinculoRepository vinculos,
    ITenantContext tenantContext,
    ITenantsApi tenants,
    ITokenService tokens,
    ITokenSenhaRepository tokensSenha,
    IIdentidadeUnitOfWork unitOfWork,
    ILinksIdentidade links,
    IEnvioEmail email,
    TimeProvider timeProvider)
    : ICommandHandler<SolicitarRedefinicaoSenhaCommand>
{
    public async ValueTask<Result> Handle(SolicitarRedefinicaoSenhaCommand command, CancellationToken cancellationToken)
    {
        UsuarioInfo? usuario = await usuarios.ObterPorEmailAsync(command.Email, cancellationToken);
        if (usuario is not { Ativo: true })
        {
            return Result.Success();
        }

        string? slug = null;
        if (tenantContext.TenantId is Guid idTenant)
        {
            UsuarioVinculo? vinculo = await vinculos.ObterAsync(usuario.Id, idTenant, cancellationToken);
            FunerariaResumoDto? funeraria = await tenants.ObterPorIdAsync(idTenant, cancellationToken);
            if (vinculo is not { Ativo: true } || funeraria is null)
            {
                return Result.Success();
            }

            slug = funeraria.Slug;
        }
        else if (!usuario.Admin)
        {
            return Result.Success();
        }

        TokenOpaco token = tokens.GerarTokenOpaco();
        tokensSenha.Adicionar(TokenSenha.Criar(
            usuario.Id, FinalidadeTokenSenha.RedefinirSenha, token.Hash, timeProvider.GetUtcNow().UtcDateTime, links.ValidadeRedefinicaoSenha));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dados = new Dictionary<string, string>
        {
            ["nome"] = usuario.Nome,
            ["link"] = links.UrlRedefinirSenha(slug, token.Valor),
            ["minutos"] = ((int)links.ValidadeRedefinicaoSenha.TotalMinutes).ToString(CultureInfo.InvariantCulture)
        };
        await email.EnfileirarAsync(
            new EmailSolicitado(ModelosEmail.RecuperacaoSenha, usuario.Email, usuario.Nome, tenantContext.TenantId, dados), cancellationToken);

        return Result.Success();
    }
}
