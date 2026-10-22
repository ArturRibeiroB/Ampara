namespace Funeraria.Modules.Identidade.Application.Abstractions;

/// <summary>
/// Links do frontend usados nos e-mails da Identidade e a validade de cada um. Vêm de configuração (Identidade).
/// O slug é o subdomínio da funerária; sem funerária (ADMIN), o link aponta para o painel da plataforma.
/// </summary>
public interface ILinksIdentidade
{
    TimeSpan ValidadeConvite { get; }

    TimeSpan ValidadeConfirmacaoEmail { get; }

    TimeSpan ValidadeRedefinicaoSenha { get; }

    string UrlDefinirSenha(string slug, string token);

    string UrlConfirmarEmail(string slug, string token);

    string UrlRedefinirSenha(string? slug, string token);

    string UrlLogin(string slug);
}
