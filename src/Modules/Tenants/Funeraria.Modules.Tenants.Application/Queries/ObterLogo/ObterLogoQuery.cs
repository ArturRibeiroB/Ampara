using Funeraria.BuildingBlocks.Application.Arquivos;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Application.Queries.ObterLogo;

/// <summary>
/// Logo da funerária, público: aparece na tela de login e nos e-mails (TEN-03, NOT-01). O armazenamento continua
/// privado; a API entrega só o arquivo do logo.
/// </summary>
public sealed record ObterLogoQuery(string Slug) : IQuery<ArquivoLido>;

public sealed class ObterLogoQueryHandler(IFunerariaRepository funerarias, IArmazenamentoArquivos armazenamento)
    : IQueryHandler<ObterLogoQuery, ArquivoLido>
{
    public async ValueTask<Result<ArquivoLido>> Handle(ObterLogoQuery query, CancellationToken cancellationToken)
    {
        FunerariaEntity? funeraria = await funerarias.ObterPorSlugAsync(query.Slug, cancellationToken);
        if (funeraria?.LogoChave is null)
        {
            return FunerariaErros.SemLogo;
        }

        ArquivoLido? logo = await armazenamento.AbrirAsync(funeraria.LogoChave, cancellationToken);
        return logo is null ? FunerariaErros.SemLogo : logo;
    }
}
