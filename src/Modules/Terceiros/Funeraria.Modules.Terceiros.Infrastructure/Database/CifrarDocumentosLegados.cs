using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Seguranca;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Terceiros.Infrastructure.Database;

/// <summary>Cifra os CPFs/CNPJs de terceiros gravados antes da cifragem (SEG-01).</summary>
internal sealed class CifrarDocumentosLegados(IDbConnectionFactory conexoes, ICriptografiaDados criptografia, ILogger<CifrarDocumentosLegados> logger)
    : CifrarDadosLegados(conexoes, criptografia, logger)
{
    protected override IReadOnlyList<ColunaCifrada> Colunas { get; } =
        [new("terceiros.terceiros", "id_terceiro", "cpf_cnpj", "cpf_cnpj_hash")];
}
