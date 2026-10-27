using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Application.Termos;
using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Vinculos;

namespace Funeraria.Modules.Identidade.Application;

public sealed class IdentidadeApi(
    IUsuarios usuarios,
    IVinculoRepository vinculos,
    IIdentidadeUnitOfWork unitOfWork,
    RegistroDeAceites aceites)
    : IIdentidadeApi
{
    public Task RegistrarAceiteTermosAsync(Guid idUsuario, CancellationToken cancellationToken = default) =>
        aceites.RegistrarVigentesAsync(idUsuario, cancellationToken);

    public async Task<IReadOnlyList<AceiteTermosDto>> ListarAceitesTermosAsync(Guid idUsuario, CancellationToken cancellationToken = default) =>
        (await aceites.ListarAsync(idUsuario, cancellationToken))
            .Select(a => new AceiteTermosDto(NomeApi.De(a.Documento), a.Versao, a.AceitoEm))
            .ToList();

    public async Task<Result<Guid>> ObterOuCriarContaAsync(string email, string nome, string senha, CancellationToken cancellationToken = default)
    {
        UsuarioInfo? existente = await usuarios.ObterPorEmailAsync(email, cancellationToken);
        if (existente is null)
        {
            Result<UsuarioInfo> criado = await usuarios.CriarComSenhaAsync(email, nome, senha, cancellationToken);
            return criado.IsSuccess ? criado.Value.Id : Result.Failure<Guid>(criado.Error);
        }

        VerificacaoCredenciais verificacao = await usuarios.VerificarCredenciaisAsync(email, senha, cancellationToken);
        return verificacao.Resultado switch
        {
            ResultadoCredenciais.Valido when !verificacao.Usuario!.Ativo => IdentidadeErros.UsuarioInativo,
            ResultadoCredenciais.Valido => verificacao.Usuario!.Id,
            ResultadoCredenciais.Bloqueado => IdentidadeErros.ContaBloqueada,
            _ => IdentidadeErros.SenhaContaExistente
        };
    }

    public async Task<string?> PerfilNaFunerariaAsync(string email, Guid idTenant, CancellationToken cancellationToken = default)
    {
        UsuarioInfo? usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);
        UsuarioVinculo? vinculo = usuario is null ? null : await vinculos.ObterAsync(usuario.Id, idTenant, cancellationToken);
        return vinculo?.Perfil.ParaClaim();
    }

    public async Task<bool> PossuiVinculoAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default) =>
        await vinculos.ObterAsync(idUsuario, idTenant, cancellationToken) is not null;

    public async Task VincularClienteAsync(Guid idUsuario, Guid idTenant, Guid idCliente, CancellationToken cancellationToken = default)
    {
        if (await vinculos.ObterAsync(idUsuario, idTenant, cancellationToken) is not null)
        {
            return;
        }

        vinculos.Adicionar(UsuarioVinculo.Criar(idUsuario, idTenant, PerfilVinculo.Cliente, idCliente: idCliente));
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ViolacaoDeUnicidadeException)
        {
            // O cadastro cria o vínculo na hora e o outbox cria de novo como garantia: se os dois gravarem
            // ao mesmo tempo, um deles encontra o vínculo já criado, que é o resultado esperado.
        }
    }
}
