using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Modules.Terceiros.Domain.Terceiros;

namespace Funeraria.Modules.Terceiros.Application.Cadastro;

public sealed class ConsultasTerceiros(ITerceiroRepository terceiros) : ITerceirosConsultas
{
    public async Task<TerceiroResumoDto?> ObterAsync(Guid idTerceiro, CancellationToken cancellationToken = default) =>
        await terceiros.ObterAsync(idTerceiro, cancellationToken) is { } t
            ? new TerceiroResumoDto(t.Id, t.NomeRazaoSocial, NomeApi.De(t.Tipo), t.Status != StatusTerceiro.Inativo, t.IdUsuario)
            : null;

    public async Task<IReadOnlyList<TerceiroResumoDto>> ListarFrotasDaRegiaoAsync(string cidade, string uf, CancellationToken cancellationToken = default) =>
        (await terceiros.ListarPorTipoAsync(TipoTerceiro.Frota, cancellationToken))
            .Where(t => t.Status != StatusTerceiro.Inativo && t.Atende(cidade, uf))
            .Select(t => new TerceiroResumoDto(t.Id, t.NomeRazaoSocial, NomeApi.De(t.Tipo), true, t.IdUsuario))
            .ToList();
}
