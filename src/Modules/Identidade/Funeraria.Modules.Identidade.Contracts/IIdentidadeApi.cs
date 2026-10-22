using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Identidade.Contracts;

/// <summary>
/// API pública da Identidade para os outros módulos: contas e vínculos (AUT-01).
/// A senha nunca sai daqui nem passa por eventos ou outbox.
/// </summary>
public interface IIdentidadeApi
{
    /// <summary>
    /// Conta para o cadastro de uma pessoa (ex.: cliente, AJU-01). Se o e-mail já tiver conta na plataforma (DEC-02),
    /// a senha precisa conferir e vale o bloqueio por tentativas do login. Senão, cria a conta com a política de senha forte.
    /// </summary>
    /// <returns>Id da conta. Falhas: senha fraca (400), senha errada da conta existente (401), conta bloqueada (423).</returns>
    Task<Result<Guid>> ObterOuCriarContaAsync(string email, string nome, string senha, CancellationToken cancellationToken = default);

    /// <summary>A conta já tem algum vínculo (de qualquer perfil) com a funerária.</summary>
    Task<bool> PossuiVinculoAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Cria o vínculo CLIENTE da conta com a funerária. Idempotente.</summary>
    Task VincularClienteAsync(Guid idUsuario, Guid idTenant, Guid idCliente, CancellationToken cancellationToken = default);
}
