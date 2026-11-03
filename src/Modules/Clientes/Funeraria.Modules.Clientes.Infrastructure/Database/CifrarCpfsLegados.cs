using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Seguranca;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Clientes.Infrastructure.Database;

/// <summary>Cifra os CPFs de clientes e dependentes gravados antes do SEG-01.</summary>
internal sealed class CifrarCpfsLegados(IDbConnectionFactory conexoes, ICriptografiaDados criptografia, ILogger<CifrarCpfsLegados> logger)
    : CifrarDadosLegados(conexoes, criptografia, logger)
{
    protected override IReadOnlyList<ColunaCifrada> Colunas { get; } =
    [
        new("clientes.clientes", "id_cliente", "cpf", "cpf_hash"),
        new("clientes.dependentes", "id_dependente", "cpf", "cpf_hash")
    ];
}
