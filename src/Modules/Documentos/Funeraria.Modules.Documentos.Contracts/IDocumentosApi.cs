namespace Funeraria.Modules.Documentos.Contracts;

/// <summary>API pública do módulo Documentos para os outros módulos. Respeita o tenant da execução atual.</summary>
public interface IDocumentosApi
{
    /// <summary>Documentos anexados ao beneficiário (CER-01).</summary>
    Task<IReadOnlyList<DocumentoResumoDto>> ListarDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default);

    /// <summary>Link temporário de um documento do beneficiário. Nulo se o documento não for dele.</summary>
    Task<LinkDocumentoDto?> GerarLinkAsync(Guid idBeneficiario, Guid idDocumento, CancellationToken cancellationToken = default);
}

/// <param name="IdDocumento">Documento.</param>
/// <param name="Tipo">Formato da API, ex.: DECLARACAO_OBITO.</param>
/// <param name="Descricao">Nome legível do tipo, ex.: Declaração de óbito.</param>
public sealed record DocumentoResumoDto(Guid IdDocumento, string Tipo, string Descricao);

public sealed record LinkDocumentoDto(Uri Url, DateTime ExpiraEm);
