using Funeraria.BuildingBlocks.Application.Messaging;

namespace Funeraria.Modules.Planos.Application.Queries.ListarPlanos;

/// <summary>
/// Lista os planos da funerária atual. O cliente vê só os ativos (Consultar Planos);
/// o gestor vê todos, para poder reativar.
/// </summary>
public sealed record ListarPlanosQuery(bool SomenteAtivos) : IQuery<IReadOnlyList<PlanoResumoResponse>>;

public sealed record PlanoResumoResponse
{
    public Guid IdPlano { get; init; }

    public required string Nome { get; init; }

    public string? Descricao { get; init; }

    public decimal ValorMensal { get; init; }

    public decimal ValorAnual { get; init; }

    public decimal ValorDependenteMensal { get; init; }

    public decimal ValorDependenteAnual { get; init; }

    public int CarenciaDias { get; init; }

    public int LimiteDependentes { get; init; }

    public bool Ativo { get; init; }
}
