using System.Globalization;
using Funeraria.BuildingBlocks.Application.Events;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Senhas;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Identidade.Application.Convites;

/// <summary>
/// Novo vínculo de equipe (gestor, funcionário ou consultor): quem ainda não tem senha recebe o convite com o link para
/// definir a senha; quem já tem conta na plataforma recebe só o aviso de acesso (DEC-02).
/// </summary>
/// <remarks>
/// Roda pelo outbox: se algo falhar antes de enfileirar o e-mail, o evento é processado de novo e um link novo é
/// gerado. O link antigo, que nunca chegou a ninguém, expira sozinho.
/// </remarks>
internal sealed class EnviarConviteDomainEventHandler(
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
        // Cliente e terceiro têm o próprio fluxo de cadastro (CLI e TER).
        if (!PerfisEquipe.Contem(evento.Perfil))
        {
            return;
        }

        UsuarioInfo? usuario = await usuarios.ObterAsync(evento.IdUsuario, cancellationToken);
        FunerariaResumoDto? funeraria = await tenants.ObterPorIdAsync(evento.IdTenant, cancellationToken);
        if (usuario is null || funeraria is null)
        {
            return;
        }

        var dados = new Dictionary<string, string>
        {
            ["nome"] = usuario.Nome,
            ["papel"] = Papel(evento.Perfil)
        };

        string modelo;
        if (await usuarios.PossuiSenhaAsync(usuario.Id, cancellationToken))
        {
            modelo = ModelosEmail.AcessoLiberado;
            dados["link"] = links.UrlLogin(funeraria.Slug);
        }
        else
        {
            TokenOpaco token = tokens.GerarTokenOpaco();
            tokensSenha.Adicionar(TokenSenha.Criar(
                usuario.Id, FinalidadeTokenSenha.DefinirSenha, token.Hash, timeProvider.GetUtcNow().UtcDateTime, links.ValidadeConvite));
            await unitOfWork.SaveChangesAsync(cancellationToken);

            modelo = ModelosEmail.Convite;
            dados["link"] = links.UrlDefinirSenha(funeraria.Slug, token.Valor);
            dados["horas"] = ((int)links.ValidadeConvite.TotalHours).ToString(CultureInfo.InvariantCulture);
        }

        await email.EnfileirarAsync(new EmailSolicitado(modelo, usuario.Email, usuario.Nome, funeraria.IdTenant, dados), cancellationToken);
    }

    private static string Papel(PerfilVinculo perfil) => perfil switch
    {
        PerfilVinculo.Funeraria => "gestor(a)",
        PerfilVinculo.Funcionario => "funcionário(a)",
        PerfilVinculo.Consultor => "consultor(a)",
        PerfilVinculo.Cliente => "cliente",
        _ => "parceiro(a)"
    };
}
