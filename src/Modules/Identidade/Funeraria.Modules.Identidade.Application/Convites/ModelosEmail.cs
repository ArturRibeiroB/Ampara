using System.Net;
using Funeraria.Modules.Identidade.Application.Abstractions;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Identidade.Application.Convites;

/// <summary>
/// Textos dos e-mails da Identidade. Ficam aqui até existir o mecanismo de templates com a marca da funerária (NOT-01).
/// </summary>
internal static class ModelosEmail
{
    public static EmailMensagem Convite(UsuarioInfo usuario, FunerariaResumoDto funeraria, PerfilVinculo perfil, string link, TimeSpan validade)
    {
        string papel = Papel(perfil);
        string horas = ((int)validade.TotalHours).ToString();

        return new EmailMensagem(
            usuario.Email,
            usuario.Nome,
            $"Convite para acessar {funeraria.NomeFantasia}",
            $"""
             <p>Olá, {H(usuario.Nome)}.</p>
             <p>Você foi convidado(a) como <strong>{H(papel)}</strong> da <strong>{H(funeraria.NomeFantasia)}</strong>.</p>
             <p><a href="{H(link)}">Clique aqui para definir sua senha</a>. O link vale por {horas} horas e só pode ser usado uma vez.</p>
             <p>Se você não esperava este convite, ignore este e-mail.</p>
             """,
            $"""
             Olá, {usuario.Nome}.

             Você foi convidado(a) como {papel} da {funeraria.NomeFantasia}.
             Defina sua senha pelo link abaixo. Ele vale por {horas} horas e só pode ser usado uma vez.

             {link}

             Se você não esperava este convite, ignore este e-mail.
             """);
    }

    public static EmailMensagem AcessoLiberado(UsuarioInfo usuario, FunerariaResumoDto funeraria, PerfilVinculo perfil, string linkLogin)
    {
        string papel = Papel(perfil);

        return new EmailMensagem(
            usuario.Email,
            usuario.Nome,
            $"Acesso liberado em {funeraria.NomeFantasia}",
            $"""
             <p>Olá, {H(usuario.Nome)}.</p>
             <p>Sua conta agora tem acesso como <strong>{H(papel)}</strong> da <strong>{H(funeraria.NomeFantasia)}</strong>.</p>
             <p><a href="{H(linkLogin)}">Entre com o seu e-mail e a senha que você já usa</a>.</p>
             """,
            $"""
             Olá, {usuario.Nome}.

             Sua conta agora tem acesso como {papel} da {funeraria.NomeFantasia}.
             Entre com o seu e-mail e a senha que você já usa: {linkLogin}
             """);
    }

    private static string Papel(PerfilVinculo perfil) => perfil switch
    {
        PerfilVinculo.Funeraria => "gestor(a)",
        PerfilVinculo.Funcionario => "funcionário(a)",
        PerfilVinculo.Cliente => "cliente",
        _ => "parceiro(a)"
    };

    private static string H(string texto) => WebUtility.HtmlEncode(texto);
}
