using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Identidade.Application.Sessoes;
using Funeraria.Modules.Identidade.Domain;
using Funeraria.Modules.Identidade.Domain.Vinculos;
using Funeraria.Modules.Tenants.Contracts;

namespace Funeraria.Modules.Identidade.Application.Queries.MinhasFunerarias;

/// <summary>Funerárias em que o usuário tem vínculo ativo, para quem é cliente de mais de uma (AUT-02).</summary>
public sealed record MinhasFunerariasQuery : IQuery<IReadOnlyList<FunerariaVinculadaResponse>>;

public sealed record FunerariaVinculadaResponse(Guid IdTenant, string Slug, string NomeFantasia, string Perfil, bool Suspensa);

public sealed class MinhasFunerariasQueryHandler(ICurrentUser currentUser, IVinculoRepository vinculos, ITenantsApi tenants)
    : IQueryHandler<MinhasFunerariasQuery, IReadOnlyList<FunerariaVinculadaResponse>>
{
    public async ValueTask<Result<IReadOnlyList<FunerariaVinculadaResponse>>> Handle(
        MinhasFunerariasQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.IdUsuario is not Guid idUsuario)
        {
            return IdentidadeErros.SessaoInvalida;
        }

        Dictionary<Guid, UsuarioVinculo> ativos = (await vinculos.ListarAtivosDoUsuarioAsync(idUsuario, cancellationToken))
            .ToDictionary(v => v.IdTenant);

        if (ativos.Count == 0)
        {
            return Array.Empty<FunerariaVinculadaResponse>();
        }

        IReadOnlyList<FunerariaResumoDto> funerarias = await tenants.ListarAsync(ativos.Keys, cancellationToken);

        return funerarias
            .Select(f => new FunerariaVinculadaResponse(f.IdTenant, f.Slug, f.NomeFantasia, ativos[f.IdTenant].Perfil.ParaClaim(), f.Suspensa))
            .ToList();
    }
}
