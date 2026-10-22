using Funeraria.BuildingBlocks.Domain.Results;

namespace Funeraria.BuildingBlocks.Application.Arquivos;

/// <summary>
/// Armazenamento de arquivos (o IFileStorage do INF-18): declaração de óbito, documentos pessoais, certidões,
/// logo e imagens do catálogo. Cada funerária tem a própria pasta, <c>tenants/{id_tenant}/</c>, e nada fica público:
/// o acesso é pela API ou por link temporário.
/// </summary>
public interface IArmazenamentoArquivos
{
    /// <summary>
    /// Valida o arquivo pela política (tipo pelo conteúdo, não pela extensão, e tamanho) e grava em
    /// <c>tenants/{idTenant}/{pasta}/</c> com um nome gerado.
    /// </summary>
    Task<Result<ArquivoSalvo>> SalvarAsync(
        Guid idTenant,
        string pasta,
        ArquivoEnviado arquivo,
        PoliticaArquivo politica,
        CancellationToken cancellationToken = default);

    /// <summary>Conteúdo do arquivo. Nulo se não existir ou se for de outra funerária que não a da requisição.</summary>
    Task<ArquivoLido?> AbrirAsync(string chave, CancellationToken cancellationToken = default);

    Task RemoverAsync(string chave, CancellationToken cancellationToken = default);

    /// <summary>Link de download que expira, para o navegador baixar direto do armazenamento.</summary>
    Task<Uri> GerarLinkTemporarioAsync(string chave, TimeSpan validade, CancellationToken cancellationToken = default);
}

/// <param name="NomeOriginal">Nome do arquivo no computador de quem enviou. Só informativo.</param>
/// <param name="Conteudo">Conteúdo enviado. O tamanho real é medido na leitura, não confiado ao cliente.</param>
public sealed record ArquivoEnviado(string NomeOriginal, Stream Conteudo);

/// <param name="Chave">Caminho no armazenamento, ex.: <c>tenants/{id}/logo/{nome}.png</c>. É o que se grava no banco.</param>
/// <param name="ContentType">Tipo reconhecido pelo conteúdo do arquivo.</param>
/// <param name="Tamanho">Tamanho em bytes.</param>
/// <param name="Hash">SHA-256 do conteúdo, em hexadecimal. Serve de versão em links e cache.</param>
public sealed record ArquivoSalvo(string Chave, string ContentType, long Tamanho, string Hash);

public sealed class ArquivoLido(Stream conteudo, string contentType, long tamanho) : IAsyncDisposable
{
    public Stream Conteudo { get; } = conteudo;

    public string ContentType { get; } = contentType;

    public long Tamanho { get; } = tamanho;

    public ValueTask DisposeAsync() => Conteudo.DisposeAsync();
}

/// <summary>Chaves dos arquivos: sempre começam pela pasta da funerária.</summary>
public static class ChavesArquivo
{
    public static string Prefixo(Guid idTenant) => $"tenants/{idTenant}/";

    public static bool PertenceA(string chave, Guid idTenant) =>
        chave.StartsWith(Prefixo(idTenant), StringComparison.Ordinal) && !chave.Contains("..", StringComparison.Ordinal);
}
