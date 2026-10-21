using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.Modules.Identidade.Domain;

/// <summary>Erros da autenticação, com as mensagens do AUT-02 e do AUT-03.</summary>
public static class IdentidadeErros
{
    /// <summary>Mesma mensagem para e-mail inexistente e senha errada: não revela se o e-mail existe.</summary>
    public static readonly Error CredenciaisInvalidas =
        Error.NaoAutorizado("Auth.CredenciaisInvalidas", "E-mail ou senha inválidos.");

    public static readonly Error UsuarioInativo =
        Error.Proibido("Auth.UsuarioInativo", "Usuário inativo.");

    public static readonly Error SemAcessoFuneraria =
        Error.Proibido("Auth.SemAcessoFuneraria", "Você não possui acesso a esta funerária.");

    public static readonly Error FunerariaNaoIdentificada =
        Error.Proibido("Auth.FunerariaNaoIdentificada", "Acesse pelo endereço da sua funerária para entrar.");

    public static readonly Error ContaBloqueada =
        Error.Bloqueado("Auth.ContaBloqueada", "Conta bloqueada temporariamente. Tente novamente mais tarde.");

    public static readonly Error SessaoInvalida =
        Error.NaoAutorizado("Auth.SessaoInvalida", "Sessão expirada. Faça login novamente.");

    /// <summary>Cadastro com e-mail que já tem conta (DEC-02): a pessoa confirma que é dona da conta pela senha.</summary>
    public static readonly Error SenhaContaExistente =
        Error.NaoAutorizado("Auth.ContaExistente", "Este e-mail já tem conta na plataforma. Informe a senha dessa conta para continuar.");

    public static readonly Error LinkSenhaInvalido =
        Error.Validacao("Auth.LinkSenhaInvalido", "Link inválido ou expirado. Peça um novo convite.");
}
