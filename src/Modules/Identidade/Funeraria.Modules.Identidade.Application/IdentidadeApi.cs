using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Application.Termos;
using Funeraria.Modules.Identidade.Contracts;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Sessoes;
using Funeraria.Modules.Identidade.Domain.Vinculos;

namespace Funeraria.Modules.Identidade.Application;

public sealed class IdentidadeApi(
    IUsuarios usuarios,
    IVinculoRepository vinculos,
    IRefreshTokenRepository refreshTokens,
    IIdentidadeUnitOfWork unitOfWork,
    RegistroDeAceites aceites,
    TimeProvider timeProvider)
    : IIdentidadeApi
{
    public Task<IReadOnlyDictionary<Guid, DateTime>> UltimosAcessosAsync(IReadOnlyCollection<Guid> idsUsuario, CancellationToken cancellationToken = default) =>
        usuarios.UltimosAcessosAsync(idsUsuario, cancellationToken);

    public async Task EncerrarAcessoDoClienteAnonimizadoAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default)
    {
        DateTime agora = timeProvider.GetUtcNow().UtcDateTime;
        UsuarioVinculo? vinculo = await vinculos.ObterAsync(idUsuario, idTenant, cancellationToken);
        if (vinculo is { Perfil: PerfilVinculo.Cliente, Ativo: true })
        {
            vinculo.AlterarStatus(false);
        }

        foreach (RefreshToken sessao in await refreshTokens.ListarAtivosDoUsuarioNaFunerariaAsync(idUsuario, idTenant, agora, cancellationToken))
        {
            sessao.Revogar(agora);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Quem também é equipe, terceiro ou cliente ativo em outra funerária continua com a conta como está.
        IReadOnlyList<UsuarioVinculo> todos = await vinculos.ListarDoUsuarioAsync(idUsuario, cancellationToken);
        UsuarioInfo? usuario = await usuarios.ObterAsync(idUsuario, cancellationToken);
        if (usuario is { Admin: false } && todos.All(v => v.Perfil == PerfilVinculo.Cliente && !v.Ativo))
        {
            await usuarios.AnonimizarAsync(idUsuario, cancellationToken);
        }
    }

    public Task<IReadOnlyList<Guid>> ListarGestoresAtivosAsync(Guid idTenant, CancellationToken cancellationToken = default) =>
        vinculos.ListarGestoresAtivosAsync(idTenant, cancellationToken);

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

    public async Task<MembroEquipeDto?> ObterMembroEquipeAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default)
    {
        UsuarioVinculo? vinculo = await vinculos.ObterAsync(idUsuario, idTenant, cancellationToken);
        if (vinculo is not { Ativo: true, Perfil: PerfilVinculo.Funeraria or PerfilVinculo.Funcionario })
        {
            return null;
        }

        UsuarioInfo? usuario = await usuarios.ObterAsync(idUsuario, cancellationToken);
        return usuario is { Ativo: true } ? new MembroEquipeDto(usuario.Id, usuario.Nome, vinculo.Perfil.ParaClaim()) : null;
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
