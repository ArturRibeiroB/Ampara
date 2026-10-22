namespace Funeraria.Modules.Notificacoes.Contracts;

/// <summary>
/// Envio de e-mails transacionais (NOT-01). Quem chama informa o modelo e os dados; o módulo Notificacoes monta o
/// e-mail com a identidade visual da funerária e envia em background pela fila "emails" do Hangfire, com retry.
/// </summary>
public interface IEnvioEmail
{
    Task EnfileirarAsync(EmailSolicitado email, CancellationToken cancellationToken = default);
}

/// <param name="Modelo">Um dos nomes em <see cref="ModelosEmail"/>.</param>
/// <param name="Para">E-mail do destinatário.</param>
/// <param name="NomeDestinatario">Nome do destinatário, usado no cabeçalho "Para".</param>
/// <param name="IdTenant">Funerária cuja identidade visual vai no e-mail. Nulo para e-mails da plataforma.</param>
/// <param name="Dados">Valores dos campos do modelo (ex.: "nome", "link"). São escapados no HTML.</param>
public sealed record EmailSolicitado(
    string Modelo,
    string Para,
    string NomeDestinatario,
    Guid? IdTenant,
    IReadOnlyDictionary<string, string> Dados);

/// <summary>
/// Modelos disponíveis. Os demais do NOT-01 (fatura gerada, lembrete de vencimento, pagamento confirmado,
/// atualização do funeral, certidão emitida) entram junto com as funcionalidades que os enviam.
/// </summary>
public static class ModelosEmail
{
    /// <summary>Convite para definir a senha. Dados: nome, papel, link, horas.</summary>
    public const string Convite = "convite";

    /// <summary>Conta existente ganhou acesso a outra funerária. Dados: nome, papel, link.</summary>
    public const string AcessoLiberado = "acesso-liberado";

    /// <summary>Boas-vindas ao cliente cadastrado. Dados: nome, link.</summary>
    public const string BoasVindas = "boas-vindas";

    /// <summary>Confirmação de e-mail (AUT-05). Dados: nome, link, horas.</summary>
    public const string ConfirmacaoEmail = "confirmacao-email";

    /// <summary>Recuperação de senha (AUT-05). Dados: nome, link, minutos.</summary>
    public const string RecuperacaoSenha = "recuperacao-senha";
}
