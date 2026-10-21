namespace Funeraria.Modules.Identidade.Application.Abstractions;

/// <summary>Links do frontend usados nos e-mails e validade do convite. Vêm de configuração (Identidade:Links).</summary>
public interface IConfiguracaoConvite
{
    TimeSpan ValidadeConvite { get; }

    string UrlDefinirSenha(string slug, string token);

    string UrlLogin(string slug);
}
