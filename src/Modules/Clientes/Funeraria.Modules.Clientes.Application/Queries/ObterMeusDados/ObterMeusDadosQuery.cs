using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Clientes;

namespace Funeraria.Modules.Clientes.Application.Queries.ObterMeusDados;

/// <summary>Dados do cliente logado (CLI-01). O id_cliente vem do token.</summary>
public sealed record ObterMeusDadosQuery : IQuery<ClienteResponse>;

public sealed class ObterMeusDadosQueryHandler(IClienteRepository clientes, ICurrentUser currentUser)
    : IQueryHandler<ObterMeusDadosQuery, ClienteResponse>
{
    public async ValueTask<Result<ClienteResponse>> Handle(ObterMeusDadosQuery query, CancellationToken cancellationToken)
    {
        Cliente? cliente = currentUser.IdCliente is Guid id ? await clientes.ObterPorIdAsync(id, cancellationToken) : null;
        return cliente is null ? ClienteErros.NaoEncontrado : ClienteResponse.De(cliente);
    }
}
