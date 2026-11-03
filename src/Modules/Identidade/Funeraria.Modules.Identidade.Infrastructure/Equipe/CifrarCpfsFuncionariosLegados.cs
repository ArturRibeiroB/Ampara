using Funeraria.BuildingBlocks.Application.Abstractions;
using Funeraria.BuildingBlocks.Infrastructure.Seguranca;
using Microsoft.Extensions.Logging;

namespace Funeraria.Modules.Identidade.Infrastructure.Equipe;

/// <summary>Cifra os CPFs de funcionários gravados antes da cifragem (SEG-01).</summary>
internal sealed class CifrarCpfsFuncionariosLegados(IDbConnectionFactory conexoes, ICriptografiaDados criptografia, ILogger<CifrarCpfsFuncionariosLegados> logger)
    : CifrarDadosLegados(conexoes, criptografia, logger)
{
    protected override IReadOnlyList<ColunaCifrada> Colunas { get; } =
        [new("identidade.funcionarios", "id_funcionario", "cpf", "cpf_hash")];
}
