using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Contratos.Contracts;

namespace Funeraria.Modules.Clientes.Application.Queries.Gestao;

/// <summary>Detalhe do cliente para a equipe, com os dependentes ativos e os contratos (CLI-03).</summary>
public sealed record ObterClienteGestaoQuery(Guid IdCliente) : IQuery<ClienteDetalheResponse>;

public sealed record ClienteDetalheResponse(
    ClienteResponse Cliente,
    IReadOnlyList<DependenteResponse> Dependentes,
    IReadOnlyList<ContratoDoClienteDto> Contratos);

public sealed class ObterClienteGestaoQueryHandler(IClienteRepository clientes, IDependenteRepository dependentes, IContratosApi contratos)
    : IQueryHandler<ObterClienteGestaoQuery, ClienteDetalheResponse>
{
    public async ValueTask<Result<ClienteDetalheResponse>> Handle(ObterClienteGestaoQuery query, CancellationToken cancellationToken)
    {
        // O filtro global restringe à funerária atual: cliente de outra funerária responde 404.
        Cliente? cliente = await clientes.ObterPorIdAsync(query.IdCliente, cancellationToken);
        if (cliente is null)
        {
            return ClienteErros.NaoEncontrado;
        }

        var lista = await dependentes.ListarDoTitularAsync(cliente.Id, cancellationToken);
        return new ClienteDetalheResponse(
            ClienteResponse.De(cliente),
            lista.Select(DependenteResponse.De).ToList(),
            await contratos.ListarDoClienteAsync(cliente.Id, cancellationToken));
    }
}
