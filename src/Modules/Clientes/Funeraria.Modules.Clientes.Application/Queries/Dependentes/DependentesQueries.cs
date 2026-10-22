using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Dependentes;

namespace Funeraria.Modules.Clientes.Application.Queries.Dependentes;

/// <summary>Dependentes ativos do titular logado (CLI-02).</summary>
public sealed record ListarDependentesQuery : IQuery<IReadOnlyList<DependenteResponse>>;

/// <summary>Um dependente do titular logado. De outro titular responde 404 (CLI-02).</summary>
public sealed record ObterDependenteQuery(Guid IdDependente) : IQuery<DependenteResponse>;

public sealed class ListarDependentesQueryHandler(IDependenteRepository dependentes, ICurrentUser currentUser)
    : IQueryHandler<ListarDependentesQuery, IReadOnlyList<DependenteResponse>>
{
    public async ValueTask<Result<IReadOnlyList<DependenteResponse>>> Handle(ListarDependentesQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.IdCliente is not Guid idCliente)
        {
            return ClienteErros.NaoEncontrado;
        }

        IReadOnlyList<Dependente> lista = await dependentes.ListarDoTitularAsync(idCliente, cancellationToken);
        return lista.Select(DependenteResponse.De).ToList();
    }
}

public sealed class ObterDependenteQueryHandler(IDependenteRepository dependentes, ICurrentUser currentUser)
    : IQueryHandler<ObterDependenteQuery, DependenteResponse>
{
    public async ValueTask<Result<DependenteResponse>> Handle(ObterDependenteQuery query, CancellationToken cancellationToken)
    {
        Dependente? dependente = currentUser.IdCliente is Guid idCliente
            ? await dependentes.ObterDoTitularAsync(query.IdDependente, idCliente, cancellationToken)
            : null;

        return dependente is null ? DependenteErros.NaoEncontrado : DependenteResponse.De(dependente);
    }
}
