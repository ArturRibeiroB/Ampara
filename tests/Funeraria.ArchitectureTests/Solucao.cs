using System.Reflection;

namespace Funeraria.ArchitectureTests;

/// <summary>Módulos e camadas da solução (mapa do INF-01). Ao criar um módulo, adicione-o aqui.</summary>
internal static class Solucao
{
    public static readonly string[] Modulos =
    [
        "Identidade", "Tenants", "Clientes", "Planos", "Catalogo", "Contratos", "Financeiro",
        "Funeral", "Documentos", "Translado", "Terceiros", "Notificacoes", "Relatorios"
    ];

    public const string Domain = "Domain";
    public const string Application = "Application";
    public const string Infrastructure = "Infrastructure";
    public const string Endpoints = "Endpoints";
    public const string Contracts = "Contracts";

    public static readonly string[] Camadas = [Domain, Application, Infrastructure, Endpoints, Contracts];

    public const string BuildingBlocksDomain = "Funeraria.BuildingBlocks.Domain";
    public const string BuildingBlocksApplication = "Funeraria.BuildingBlocks.Application";
    public const string BuildingBlocksInfrastructure = "Funeraria.BuildingBlocks.Infrastructure";

    public static string Namespace(string modulo, string camada) => $"Funeraria.Modules.{modulo}.{camada}";

    public static Assembly Assembly(string modulo, string camada) =>
        System.Reflection.Assembly.Load(Namespace(modulo, camada));

    public static Assembly[] AssembliesDoModulo(string modulo) =>
        Camadas.Select(c => Assembly(modulo, c)).ToArray();

    public static TheoryData<string> TodosOsModulos() => new(Modulos);
}
