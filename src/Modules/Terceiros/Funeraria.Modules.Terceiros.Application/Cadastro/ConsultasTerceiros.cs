using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Terceiros.Contracts;
using Funeraria.Modules.Terceiros.Domain.Terceiros;

namespace Funeraria.Modules.Terceiros.Application.Cadastro;

public sealed class ConsultasTerceiros(ITerceiroRepository terceiros) : ITerceirosConsultas
{
    public async Task<TerceiroResumoDto?> ObterAsync(Guid idTerceiro, CancellationToken cancellationToken = default) =>
        await terceiros.ObterAsync(idTerceiro, cancellationToken) is { } t
            ? new TerceiroResumoDto(t.Id, t.NomeRazaoSocial, NomeApi.De(t.Tipo), t.Status != StatusTerceiro.Inativo)
            : null;
}
