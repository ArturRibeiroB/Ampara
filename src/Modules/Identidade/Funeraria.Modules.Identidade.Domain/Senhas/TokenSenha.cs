using Funeraria.BuildingBlocks.Domain.Entities;

namespace Funeraria.Modules.Identidade.Domain.Senhas;

/// <summary>
/// Token de uso único para definir ou redefinir a senha (convite do TEN-02/AUT-07 e recuperação do AUT-05).
/// Só o hash é gravado; o valor vai apenas no link enviado por e-mail.
/// </summary>
public sealed class TokenSenha : AggregateRoot<Guid>
{
    private TokenSenha(Guid id)
        : base(id)
    {
    }

    private TokenSenha()
    {
    }

    public Guid IdUsuario { get; private set; }

    public FinalidadeTokenSenha Finalidade { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime CriadoEm { get; private set; }

    public DateTime ExpiraEm { get; private set; }

    public DateTime? UsadoEm { get; private set; }

    public static TokenSenha Criar(Guid idUsuario, FinalidadeTokenSenha finalidade, string tokenHash, DateTime agora, TimeSpan validade) =>
        new(Guid.CreateVersion7())
        {
            IdUsuario = idUsuario,
            Finalidade = finalidade,
            TokenHash = tokenHash,
            CriadoEm = agora,
            ExpiraEm = agora + validade
        };

    public bool EstaValido(DateTime agora) => UsadoEm is null && agora < ExpiraEm;

    public void MarcarUsado(DateTime agora)
    {
        if (!EstaValido(agora))
        {
            throw new InvalidOperationException("Token de senha já usado ou expirado.");
        }

        UsadoEm = agora;
    }
}

public enum FinalidadeTokenSenha
{
    /// <summary>Convite: a conta ainda não tem senha. Definir a senha também confirma o e-mail.</summary>
    DefinirSenha = 1,

    /// <summary>"Esqueci minha senha" (AUT-05).</summary>
    RedefinirSenha = 2
}

public interface ITokenSenhaRepository
{
    Task<TokenSenha?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    void Adicionar(TokenSenha token);
}
