using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Application.Messaging;
using Funeraria.BuildingBlocks.Domain.Results;
using Funeraria.Modules.Clientes.Domain;
using Funeraria.Modules.Clientes.Domain.Clientes;
using Funeraria.Modules.Clientes.Domain.Dependentes;
using Funeraria.Modules.Contratos.Contracts;
using Funeraria.Modules.Identidade.Contracts;

namespace Funeraria.Modules.Clientes.Application.Queries.ExportarMeusDados;

/// <summary>
/// O titular baixa os próprios dados guardados nesta funerária (SEG-01, LGPD art. 18): cadastro, dependentes, contratos e
/// aceites de termos. Cada módulo entrega a sua parte pela própria API.
/// </summary>
public sealed record ExportarMeusDadosQuery : IQuery<ExportacaoDadosResponse>;

/// <param name="GeradoEm">Quando a exportação foi gerada (UTC).</param>
/// <param name="Titular">Cadastro do titular.</param>
/// <param name="Dependentes">Dependentes cadastrados pelo titular.</param>
/// <param name="Contratos">Contratos do titular nesta funerária.</param>
/// <param name="AceitesTermos">Histórico de aceite dos termos de uso e da política de privacidade.</param>
public sealed record ExportacaoDadosResponse(
    DateTime GeradoEm,
    ClienteResponse Titular,
    IReadOnlyList<DependenteResponse> Dependentes,
    IReadOnlyList<ContratoDoClienteDto> Contratos,
    IReadOnlyList<AceiteTermosDto> AceitesTermos);

public sealed class ExportarMeusDadosQueryHandler(
    IClienteRepository clientes,
    IDependenteRepository dependentes,
    IContratosApi contratos,
    IIdentidadeApi identidade,
    ICurrentUser usuario,
    TimeProvider timeProvider)
    : IQueryHandler<ExportarMeusDadosQuery, ExportacaoDadosResponse>
{
    public async ValueTask<Result<ExportacaoDadosResponse>> Handle(ExportarMeusDadosQuery query, CancellationToken cancellationToken)
    {
        Cliente? cliente = usuario.IdCliente is Guid id ? await clientes.ObterPorIdAsync(id, cancellationToken) : null;
        if (cliente is null)
        {
            return ClienteErros.NaoEncontrado;
        }

        IReadOnlyList<Dependente> doTitular = await dependentes.ListarDoTitularAsync(cliente.Id, cancellationToken);
        return new ExportacaoDadosResponse(
            timeProvider.GetUtcNow().UtcDateTime,
            ClienteResponse.De(cliente),
            [.. doTitular.Select(DependenteResponse.De)],
            await contratos.ListarDoClienteAsync(cliente.Id, cancellationToken),
            await identidade.ListarAceitesTermosAsync(cliente.IdUsuario, cancellationToken));
    }
}
