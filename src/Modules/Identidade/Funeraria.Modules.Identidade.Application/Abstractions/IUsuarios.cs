using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Identidade.Application.Abstractions;

/// <summary>
/// Contas de usuário. Implementado com o ASP.NET Core Identity na Infrastructure, que cuida do hash da senha
/// e do bloqueio por tentativas (AUT-01).
/// </summary>
public interface IUsuarios
{
    /// <summary>
    /// Confere e-mail e senha. Senha errada conta uma tentativa para o bloqueio; senha certa zera o contador.
    /// </summary>
    Task<VerificacaoCredenciais> VerificarCredenciaisAsync(string email, string senha, CancellationToken cancellationToken = default);

    Task<UsuarioInfo?> ObterAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    Task<UsuarioInfo?> ObterPorEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cria uma conta convidada: sem senha e com o e-mail ainda não confirmado. A pessoa define a senha
    /// pelo link do convite, o que também confirma o e-mail.
    /// </summary>
    Task<UsuarioInfo> CriarConvidadoAsync(string email, string nome, CancellationToken cancellationToken = default);

    Task<bool> PossuiSenhaAsync(Guid idUsuario, CancellationToken cancellationToken = default);

    /// <summary>Define a senha pela política de senha forte e confirma o e-mail. Senha fraca vira erro de validação.</summary>
    Task<Result> DefinirSenhaAsync(Guid idUsuario, string senha, CancellationToken cancellationToken = default);
}

public sealed record UsuarioInfo(Guid Id, string Nome, string Email, bool Ativo, bool Bloqueado, bool Admin);

public enum ResultadoCredenciais
{
    Valido = 1,
    Invalido = 2,
    Bloqueado = 3
}

public sealed record VerificacaoCredenciais(ResultadoCredenciais Resultado, UsuarioInfo? Usuario)
{
    public static readonly VerificacaoCredenciais Invalido = new(ResultadoCredenciais.Invalido, null);

    public static readonly VerificacaoCredenciais Bloqueado = new(ResultadoCredenciais.Bloqueado, null);
}
