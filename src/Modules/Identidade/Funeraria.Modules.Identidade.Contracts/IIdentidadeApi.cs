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

    /// <summary>Perfil (ex.: CLIENTE, FUNCIONARIO) que o e-mail já tem nesta funerária; nulo se não tiver acesso.</summary>
    Task<string?> PerfilNaFunerariaAsync(string email, Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Registra o aceite das versões vigentes dos termos de uso e da política de privacidade (SEG-01).</summary>
    Task RegistrarAceiteTermosAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Histórico de aceites da conta, para a exportação dos dados pelo titular (SEG-01).</summary>
    Task<IReadOnlyList<AceiteTermosDto>> ListarAceitesTermosAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    /// <summary>A conta já tem algum vínculo (de qualquer perfil) com a funerária.</summary>
    Task<bool> PossuiVinculoAsync(Guid idUsuario, Guid idTenant, CancellationToken cancellationToken = default);

    /// <summary>Cria o vínculo CLIENTE da conta com a funerária. Idempotente.</summary>
    Task VincularClienteAsync(Guid idUsuario, Guid idTenant, Guid idCliente, CancellationToken cancellationToken = default);
}

/// <summary>Aceite de um documento legal. Documento: TERMOS_DE_USO ou POLITICA_DE_PRIVACIDADE.</summary>
public sealed record AceiteTermosDto(string Documento, string Versao, DateTime AceitoEm);
