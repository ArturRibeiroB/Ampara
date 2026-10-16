using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Vinculos;

namespace Funeraria.Modules.Identidade.Application.Commands.Login;

public sealed class LoginCommandHandler(
    IUsuarios usuarios,
    IVinculoRepository vinculos,
    ITenantContext tenantContext,
    EmissorSessao emissor,
    IIdentidadeUnitOfWork unitOfWork)
    : ICommandHandler<LoginCommand, SessaoResponse>
{
    public async ValueTask<Result<SessaoResponse>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        VerificacaoCredenciais verificacao = await usuarios.VerificarCredenciaisAsync(command.Email.Trim(), command.Senha, cancellationToken);

        switch (verificacao.Resultado)
        {
            case ResultadoCredenciais.Bloqueado:
                return IdentidadeErros.ContaBloqueada;
            case ResultadoCredenciais.Invalido:
                return IdentidadeErros.CredenciaisInvalidas;
        }

        UsuarioInfo usuario = verificacao.Usuario!;
        if (!usuario.Ativo)
        {
            return IdentidadeErros.UsuarioInativo;
        }

        UsuarioVinculo? vinculo = null;
        if (tenantContext.TenantId is Guid idTenant)
        {
            vinculo = await vinculos.ObterAsync(usuario.Id, idTenant, cancellationToken);
            if (vinculo is not { Ativo: true })
            {
                return IdentidadeErros.SemAcessoFuneraria;
            }
        }
        else if (!usuario.Admin)
        {
            return IdentidadeErros.FunerariaNaoIdentificada;
        }

        SessaoResponse sessao = emissor.Emitir(usuario, vinculo, anterior: null);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return sessao;
    }
}
