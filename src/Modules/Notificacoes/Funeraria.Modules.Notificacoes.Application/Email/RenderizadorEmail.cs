using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.RegularExpressions;
using Funeraria.Modules.Notificacoes.Contracts;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Notificacoes.Application.Email;

/// <summary>E-mail pronto para envio.</summary>
public sealed record EmailMensagem(string Para, string NomeDestinatario, string Assunto, string CorpoHtml, string CorpoTexto);

/// <summary>Quem efetivamente entrega o e-mail (SMTP). Nos testes, é trocado por um coletor.</summary>
public interface IEmailTransporte
{
    Task EnviarAsync(EmailMensagem mensagem, CancellationToken cancellationToken = default);
}

/// <summary>
/// Monta o e-mail a partir do modelo e da identidade visual da funerária (NOT-01).
/// <c>{{campo}}</c> é sempre escapado; <c>{{{campo}}}</c> (sem escape) só é usado no layout, com blocos montados aqui.
/// Campo sem valor é erro de programação e falha a renderização, em vez de sair "{{nome}}" para o cliente.
/// </summary>
public sealed partial class RenderizadorEmail(ITenantsApi tenants)
{
    private const string NomePlataforma = "Funerária Online";
    private const string CorPlataforma = "#1F3A5F";

    // Escapa <, >, & e aspas, mas mantém os acentos como texto (o HtmlEncode padrão os troca por entidades).
    private static readonly HtmlEncoder Html = HtmlEncoder.Create(UnicodeRanges.All);

    private static readonly Dictionary<string, string> Assuntos = new()
    {
        [ModelosEmail.Convite] = "Convite para acessar {{nome_funeraria}}",
        [ModelosEmail.AcessoLiberado] = "Acesso liberado em {{nome_funeraria}}",
        [ModelosEmail.BoasVindas] = "Bem-vindo(a) à {{nome_funeraria}}",
        [ModelosEmail.ConfirmacaoEmail] = "Confirme seu e-mail",
        [ModelosEmail.RecuperacaoSenha] = "Redefinição de senha"
    };

    public static IReadOnlyCollection<string> Modelos => Assuntos.Keys;

    public async Task<EmailMensagem> RenderizarAsync(EmailSolicitado solicitado, CancellationToken cancellationToken = default)
    {
        if (!Assuntos.TryGetValue(solicitado.Modelo, out string? assuntoModelo))
        {
            throw new InvalidOperationException($"Modelo de e-mail desconhecido: '{solicitado.Modelo}'.");
        }

        MarcaFunerariaDto? marca = solicitado.IdTenant is Guid idTenant
            ? await tenants.ObterMarcaAsync(idTenant, cancellationToken)
            : null;

        var dados = new Dictionary<string, string>(solicitado.Dados, StringComparer.Ordinal)
        {
            ["nome_funeraria"] = marca?.NomeExibicao ?? NomePlataforma,
            ["cor_primaria"] = marca?.CorPrimaria ?? CorPlataforma
        };

        string assunto = Preencher(assuntoModelo, dados, html: false);
        dados["assunto"] = assunto;

        string conteudo = Preencher(Ler($"{solicitado.Modelo}.html"), dados, html: true);
        var layout = new Dictionary<string, string>(dados, StringComparer.Ordinal)
        {
            ["conteudo"] = conteudo,
            ["cabecalho"] = Cabecalho(marca)
        };

        return new EmailMensagem(
            solicitado.Para,
            solicitado.NomeDestinatario,
            assunto,
            Preencher(Ler("layout.html"), layout, html: true),
            Preencher(Ler($"{solicitado.Modelo}.txt"), dados, html: false));
    }

    /// <summary>Logo da funerária, se houver; senão, o nome. Valores escapados aqui, porque o bloco entra sem escape.</summary>
    private static string Cabecalho(MarcaFunerariaDto? marca)
    {
        string nome = Html.Encode(marca?.NomeExibicao ?? NomePlataforma);
        return string.IsNullOrWhiteSpace(marca?.LogoUrl)
            ? nome
            : $"""<img src="{Html.Encode(marca.LogoUrl)}" alt="{nome}" style="max-height:48px;">""";
    }

    /// <summary>
    /// Uma única passada: o valor inserido nunca é lido de novo. Assim um dado do usuário com "{{...}}"
    /// (ex.: um nome digitado assim) sai como texto e não vira campo do modelo.
    /// </summary>
    private static string Preencher(string modelo, IReadOnlyDictionary<string, string> dados, bool html) =>
        Campo().Replace(modelo, m => m.Groups["bruto"].Success
            ? Valor(dados, m.Groups["bruto"].Value)
            : html ? Html.Encode(Valor(dados, m.Groups["campo"].Value)) : Valor(dados, m.Groups["campo"].Value));

    private static string Valor(IReadOnlyDictionary<string, string> dados, string campo) =>
        dados.TryGetValue(campo, out string? valor)
            ? valor
            : throw new InvalidOperationException($"O modelo de e-mail usa o campo '{campo}', que não foi informado.");

    private static string Ler(string arquivo)
    {
        Assembly assembly = typeof(RenderizadorEmail).Assembly;
        string recurso = $"{assembly.GetName().Name}.Email.Modelos.{arquivo}";
        using Stream stream = assembly.GetManifestResourceStream(recurso)
            ?? throw new InvalidOperationException($"Arquivo de modelo de e-mail não encontrado: {arquivo}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"\{\{\{\s*(?<bruto>[a-z_]+)\s*\}\}\}|\{\{\s*(?<campo>[a-z_]+)\s*\}\}")]
    private static partial Regex Campo();
}
