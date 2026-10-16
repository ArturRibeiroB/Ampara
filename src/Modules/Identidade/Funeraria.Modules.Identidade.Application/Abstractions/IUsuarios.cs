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
