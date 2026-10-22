using System.Globalization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Identidade.Application.Convites;

/// <summary>
/// Cliente cadastrado na funerária (CLI-01): conta com e-mail ainda não confirmado recebe o link de confirmação
/// (AUT-05); conta que já tinha o e-mail confirmado recebe as boas-vindas.
/// </summary>
/// <remarks>Roda pelo outbox, como o convite da equipe.</remarks>
internal sealed class EnviarBoasVindasClienteDomainEventHandler(
    IUsuarios usuarios,
    ITenantsApi tenants,
    ITokenService tokens,
    ITokenSenhaRepository tokensSenha,
    IIdentidadeUnitOfWork unitOfWork,
    ILinksIdentidade links,
    IEnvioEmail email,
    TimeProvider timeProvider)
    : IDomainEventHandler<VinculoCriadoDomainEvent>
{
    public async Task Handle(VinculoCriadoDomainEvent evento, CancellationToken cancellationToken)
    {
        if (evento.Perfil != PerfilVinculo.Cliente)
        {
            return;
        }

        UsuarioInfo? usuario = await usuarios.ObterAsync(evento.IdUsuario, cancellationToken);
        FunerariaResumoDto? funeraria = await tenants.ObterPorIdAsync(evento.IdTenant, cancellationToken);
        if (usuario is null || funeraria is null)
        {
            return;
        }

        var dados = new Dictionary<string, string> { ["nome"] = usuario.Nome };

        string modelo;
        if (usuario.EmailConfirmado)
        {
            modelo = ModelosEmail.BoasVindas;
            dados["link"] = links.UrlLogin(funeraria.Slug);
        }
        else
        {
            TokenOpaco token = tokens.GerarTokenOpaco();
            tokensSenha.Adicionar(TokenSenha.Criar(
                usuario.Id, FinalidadeTokenSenha.ConfirmarEmail, token.Hash, timeProvider.GetUtcNow().UtcDateTime, links.ValidadeConfirmacaoEmail));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            modelo = ModelosEmail.ConfirmacaoEmail;
            dados["link"] = links.UrlConfirmarEmail(funeraria.Slug, token.Valor);
            dados["horas"] = ((int)links.ValidadeConfirmacaoEmail.TotalHours).ToString(CultureInfo.InvariantCulture);
        }

        await email.EnfileirarAsync(new EmailSolicitado(modelo, usuario.Email, usuario.Nome, funeraria.IdTenant, dados), cancellationToken);
    }
}
