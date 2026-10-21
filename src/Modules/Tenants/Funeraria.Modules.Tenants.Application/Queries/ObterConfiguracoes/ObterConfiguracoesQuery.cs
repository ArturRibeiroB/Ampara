using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Tenants.Domain.Funerarias;
using FunerariaEntity = Funeraria.Modules.Tenants.Domain.Funerarias.Funeraria;

namespace Funeraria.Modules.Tenants.Application.Queries.ObterConfiguracoes;

/// <summary>Configurações completas da funerária atual, para o gestor (TEN-03).</summary>
public sealed record ObterConfiguracoesQuery : IQuery<ConfiguracoesResponse>;

/// <summary>Identidade visual da funerária atual, pública: o frontend usa para montar a tela (TEN-03).</summary>
public sealed record ObterIdentidadeVisualQuery : IQuery<IdentidadeVisualResponse>;

public sealed record ConfiguracoesResponse(
    Guid IdTenant,
    string NomeFantasia,
    string Slug,
    StatusFuneraria Status,
    IdentidadeVisual IdentidadeVisual,
    ParametrosFuneraria Parametros,
    bool CobrancaConfigurada);

public sealed record IdentidadeVisualResponse(string Slug, string NomeFantasia, IdentidadeVisual IdentidadeVisual);

public sealed class ObterConfiguracoesQueryHandler(IFunerariaRepository funerarias, ITenantContext tenantContext)
    : IQueryHandler<ObterConfiguracoesQuery, ConfiguracoesResponse>
{
    public async ValueTask<Result<ConfiguracoesResponse>> Handle(ObterConfiguracoesQuery query, CancellationToken cancellationToken)
    {
        FunerariaEntity? f = await funerarias.ObterPorIdAsync(tenantContext.RequiredTenantId, cancellationToken);
        if (f is null)
        {
            return FunerariaErros.NaoEncontrada;
        }

        return new ConfiguracoesResponse(
            f.Id, f.NomeFantasia, f.Slug, f.Status, f.IdentidadeVisual, f.Parametros, f.Parametros.Cobranca.EstaCompleta());
    }
}

public sealed class ObterIdentidadeVisualQueryHandler(IFunerariaRepository funerarias, ITenantContext tenantContext)
    : IQueryHandler<ObterIdentidadeVisualQuery, IdentidadeVisualResponse>
{
    public async ValueTask<Result<IdentidadeVisualResponse>> Handle(ObterIdentidadeVisualQuery query, CancellationToken cancellationToken)
    {
        FunerariaEntity? f = await funerarias.ObterPorIdAsync(tenantContext.RequiredTenantId, cancellationToken);
        return f is null
            ? FunerariaErros.NaoEncontrada
            : new IdentidadeVisualResponse(f.Slug, f.NomeFantasia, f.IdentidadeVisual);
    }
}
