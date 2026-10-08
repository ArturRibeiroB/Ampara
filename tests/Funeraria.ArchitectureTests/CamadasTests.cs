using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;
using static Funeraria.ArchitectureTests.Solucao;

namespace Funeraria.ArchitectureTests;

/// <summary>Regras da Clean Architecture dentro de cada módulo e nos BuildingBlocks.</summary>
public sealed class CamadasTests
{
    private static readonly string[] Frameworks =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "Dapper"
    ];

    [Fact]
    public void BuildingBlocks_Domain_nao_depende_de_nada()
    {
        TestResult result = Types.InAssembly(System.Reflection.Assembly.Load(BuildingBlocksDomain))
            .ShouldNot()
            .HaveDependencyOnAny([BuildingBlocksApplication, BuildingBlocksInfrastructure, "Mediator", "FluentValidation", .. Frameworks])
            .GetResult();

        ArchAssert.Passou(result, "BuildingBlocks.Domain deve ser puro.");
    }

    [Fact]
    public void BuildingBlocks_Application_nao_depende_de_infraestrutura()
    {
        TestResult result = Types.InAssembly(System.Reflection.Assembly.Load(BuildingBlocksApplication))
            .ShouldNot()
            .HaveDependencyOnAny([BuildingBlocksInfrastructure, .. Frameworks])
            .GetResult();

        ArchAssert.Passou(result, "BuildingBlocks.Application não pode depender de infraestrutura.");
    }

    [Theory]
    [MemberData(nameof(TodosOsModulos), MemberType = typeof(Solucao))]
    public void Domain_nao_depende_de_outras_camadas_nem_de_frameworks(string modulo)
    {
        TestResult result = Types.InAssembly(Assembly(modulo, Domain))
            .ShouldNot()
            .HaveDependencyOnAny(
            [
                Namespace(modulo, Application),
                Namespace(modulo, Infrastructure),
                Namespace(modulo, Endpoints),
                Namespace(modulo, Contracts),
                BuildingBlocksApplication,
                BuildingBlocksInfrastructure,
                "Mediator",
                "FluentValidation",
                .. Frameworks
            ])
            .GetResult();

        ArchAssert.Passou(result, $"{modulo}.Domain deve depender só de BuildingBlocks.Domain.");
    }

    [Theory]
    [MemberData(nameof(TodosOsModulos), MemberType = typeof(Solucao))]
    public void Application_nao_depende_de_Infrastructure_nem_de_Endpoints(string modulo)
    {
        TestResult result = Types.InAssembly(Assembly(modulo, Application))
            .ShouldNot()
            .HaveDependencyOnAny(
                Namespace(modulo, Infrastructure),
                Namespace(modulo, Endpoints),
                BuildingBlocksInfrastructure,
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql")
            .GetResult();

        ArchAssert.Passou(result, $"{modulo}.Application não pode depender de infraestrutura nem de HTTP.");
    }

    [Theory]
    [MemberData(nameof(TodosOsModulos), MemberType = typeof(Solucao))]
    public void Endpoints_nao_acessam_o_banco_diretamente(string modulo)
    {
        TestResult result = Types.InAssembly(Assembly(modulo, Endpoints))
            .ShouldNot()
            .HaveDependencyOnAny(
                Namespace(modulo, Infrastructure),
                "Microsoft.EntityFrameworkCore",
                "Npgsql",
                "Dapper")
            .GetResult();

        ArchAssert.Passou(result, $"{modulo}.Endpoints deve chamar a Application via Mediator, sem acessar o banco.");
    }

    [Theory]
    [MemberData(nameof(TodosOsModulos), MemberType = typeof(Solucao))]
    public void Handlers_devem_ser_publicos_e_selados(string modulo)
    {
        // O Mediator gera o registro no host, então os handlers precisam ser públicos.
        TestResult result = Types.InAssembly(Assembly(modulo, Application))
            .That()
            .HaveNameEndingWith("CommandHandler")
            .Or()
            .HaveNameEndingWith("QueryHandler")
            .Should()
            .BePublic()
            .And()
            .BeSealed()
            .GetResult();

        ArchAssert.Passou(result, $"Handlers de {modulo} devem ser public sealed.");
    }
}
