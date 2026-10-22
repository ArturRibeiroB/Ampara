using System.Security.Cryptography;
using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.Application.Arquivos;

/// <summary>Tipos aceitos. SVG fica de fora de propósito: pode carregar script.</summary>
public enum TipoArquivo
{
    Png = 1,
    Jpeg = 2,
    Webp = 3,
    Pdf = 4
}

/// <summary>O que cada uso aceita (INF-18). O tipo é reconhecido pelos primeiros bytes do arquivo.</summary>
public sealed record PoliticaArquivo(long TamanhoMaximo, IReadOnlySet<TipoArquivo> Tipos)
{
    private const long Mb = 1024 * 1024;

    /// <summary>Logo da funerária (TEN-03).</summary>
    public static readonly PoliticaArquivo Logo = new(2 * Mb, new HashSet<TipoArquivo> { TipoArquivo.Png, TipoArquivo.Jpeg, TipoArquivo.Webp });

    /// <summary>Imagens do catálogo de produtos e serviços.</summary>
    public static readonly PoliticaArquivo Imagem = new(5 * Mb, new HashSet<TipoArquivo> { TipoArquivo.Png, TipoArquivo.Jpeg, TipoArquivo.Webp });

    /// <summary>Documentos enviados (declaração de óbito, documentos pessoais): PDF ou foto.</summary>
    public static readonly PoliticaArquivo Documento = new(10 * Mb, new HashSet<TipoArquivo> { TipoArquivo.Pdf, TipoArquivo.Png, TipoArquivo.Jpeg });
}

/// <summary>Arquivo lido por inteiro e aprovado pela política, pronto para gravar.</summary>
public sealed record ArquivoValidado(byte[] Conteudo, TipoArquivo Tipo, string ContentType, string Extensao, string Hash);

public static class ValidadorArquivo
{
    public const string Campo = "arquivo";

    /// <summary>
    /// Lê o conteúdo com limite (um arquivo maior que o permitido não é lido além do limite) e confere o tipo
    /// pelos primeiros bytes. Nome e content-type informados pelo cliente não são considerados.
    /// </summary>
    public static async Task<Result<ArquivoValidado>> ValidarAsync(
        ArquivoEnviado arquivo,
        PoliticaArquivo politica,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        byte[] bloco = new byte[81920];
        int lidos;
        while ((lidos = await arquivo.Conteudo.ReadAsync(bloco, cancellationToken)) > 0)
        {
            buffer.Write(bloco, 0, lidos);
            if (buffer.Length > politica.TamanhoMaximo)
            {
                return Erro($"Arquivo maior que o permitido (máximo de {politica.TamanhoMaximo / (1024 * 1024)} MB).");
            }
        }

        if (buffer.Length == 0)
        {
            return Erro("Arquivo vazio.");
        }

        byte[] conteudo = buffer.ToArray();
        TipoArquivo? tipo = Identificar(conteudo);
        if (tipo is null || !politica.Tipos.Contains(tipo.Value))
        {
            string aceitos = string.Join(", ", politica.Tipos.Order().Select(t => t.ToString().ToUpperInvariant()));
            return Erro($"Tipo de arquivo não permitido. Envie {aceitos}.");
        }

        (string contentType, string extensao) = tipo.Value switch
        {
            TipoArquivo.Png => ("image/png", ".png"),
            TipoArquivo.Jpeg => ("image/jpeg", ".jpg"),
            TipoArquivo.Webp => ("image/webp", ".webp"),
            _ => ("application/pdf", ".pdf")
        };

        string hash = Convert.ToHexStringLower(SHA256.HashData(conteudo));
        return new ArquivoValidado(conteudo, tipo.Value, contentType, extensao, hash);
    }

    /// <summary>Assinatura dos primeiros bytes de cada formato.</summary>
    public static TipoArquivo? Identificar(ReadOnlySpan<byte> c)
    {
        if (c.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return TipoArquivo.Png;
        }

        if (c.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return TipoArquivo.Jpeg;
        }

        if (c.Length >= 12 && c[..4].SequenceEqual("RIFF"u8) && c[8..12].SequenceEqual("WEBP"u8))
        {
            return TipoArquivo.Webp;
        }

        if (c.StartsWith("%PDF-"u8))
        {
            return TipoArquivo.Pdf;
        }

        return null;
    }

    private static ValidationError Erro(string mensagem) =>
        new(new Dictionary<string, string[]> { [Campo] = [mensagem] });
}
