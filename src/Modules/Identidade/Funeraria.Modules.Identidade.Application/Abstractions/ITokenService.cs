namespace Funeraria.Modules.Identidade.Application.Abstractions;

/// <summary>Emissão do access token JWT e geração dos tokens opacos: refresh token e link de senha (AUT-02, AUT-03).</summary>
public interface ITokenService
{
    TimeSpan DuracaoRefreshToken { get; }

    AccessToken GerarAccessToken(DadosToken dados);

    /// <summary>Valor aleatório (refresh token, link de senha) e o hash que vai para o banco.</summary>
    TokenOpaco GerarTokenOpaco();

    string Hash(string token);
}

/// <summary>Claims do token (AUT-02): sub, email, role, id_tenant, id_cliente, id_terceiro, tipo_terceiro.</summary>
public sealed record DadosToken(
    Guid IdUsuario,
    string Email,
    string Nome,
    string Perfil,
    Guid? IdTenant,
    Guid? IdCliente,
    Guid? IdTerceiro,
    string? TipoTerceiro);

public sealed record AccessToken(string Token, DateTime ExpiraEm);

public sealed record TokenOpaco(string Valor, string Hash);
