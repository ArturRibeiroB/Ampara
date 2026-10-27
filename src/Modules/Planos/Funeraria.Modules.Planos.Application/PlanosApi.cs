using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Planos.Application.Queries.ObterPlano;
using Funeraria.Modules.Planos.Contracts;
using Funeraria.Modules.Planos.Domain.Planos;
using Mediator;

namespace Funeraria.Modules.Planos.Application;

public sealed class PlanosApi(ISender sender, IPlanoRepository planos) : IPlanosApi
{
    public async Task<IReadOnlyList<CoberturaPlanoDto>> ObterCoberturasAsync(Guid idPlano, CancellationToken cancellationToken = default) =>
        await planos.ObterPorIdAsync(idPlano, cancellationToken) is Plano plano
            ? [.. plano.Coberturas.Select(c => new CoberturaPlanoDto(c.IdItemCatalogo, c.Quantidade, c.KmTransladoIncluso))]
            : [];

    public async Task<PlanoDto?> ObterPlanoAsync(Guid idPlano, CancellationToken cancellationToken = default)
    {
        Result<PlanoResponse> result = await sender.Send(new ObterPlanoQuery(idPlano, SomenteAtivos: false), cancellationToken);
        if (result.IsFailure)
        {
            return null;
        }

        PlanoResponse plano = result.Value;
        return new PlanoDto(
            plano.IdPlano,
            plano.Nome,
            plano.ValorMensal,
            plano.ValorAnual,
            plano.ValorDependenteMensal,
            plano.ValorDependenteAnual,
            plano.CarenciaDias,
            plano.LimiteDependentes,
            plano.PermiteSomenteDependente,
            plano.Ativo);
    }
}
