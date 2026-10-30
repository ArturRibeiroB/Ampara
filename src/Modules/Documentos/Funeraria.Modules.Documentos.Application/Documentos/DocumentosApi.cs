using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Documentos.Application.Certidoes;
using Funeraria.Modules.Documentos.Contracts;
using Funeraria.Modules.Documentos.Domain.Documentos;

namespace Funeraria.Modules.Documentos.Application.Documentos;

public sealed class DocumentosApi(IDocumentoRepository documentos, LinksDeDocumentos links) : IDocumentosApi
{
    public async Task<IReadOnlyList<DocumentoResumoDto>> ListarDoBeneficiarioAsync(Guid idBeneficiario, CancellationToken cancellationToken = default) =>
        (await documentos.ListarDoBeneficiarioAsync(idBeneficiario, cancellationToken))
            .Select(d => new DocumentoResumoDto(d.Id, NomeApi.De(d.Tipo), DocumentosExigidos.Descricao(d.Tipo)))
            .ToList();

    public async Task<LinkDocumentoDto?> GerarLinkAsync(Guid idBeneficiario, Guid idDocumento, CancellationToken cancellationToken = default)
    {
        Documento? documento = await documentos.ObterAsync(idDocumento, cancellationToken);
        if (documento is null || documento.IdBeneficiario != idBeneficiario)
        {
            return null;
        }

        LinkDocumentoResponse link = await links.GerarAsync(documento, cancellationToken);
        return new LinkDocumentoDto(link.Url, link.ExpiraEm);
    }
}
