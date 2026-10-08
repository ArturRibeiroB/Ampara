using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;
using static Funeraria.ArchitectureTests.Solucao;

namespace Funeraria.ArchitectureTests;

/// <summary>Regras do monolito modular (INF-01): um módulo só enxerga o Contracts dos outros.</summary>
public sealed class FronteirasEntreModulosTests
{
    [Theory]
    [MemberData(nameof(TodosOsModulos), MemberType = typeof(Solucao))]
    public void Modulo_so_pode_depender_do_Contracts_de_outro_modulo(string modulo)
    {
        string[] proibidos = Modulos
            .Where(outro => outro != modulo)
            .SelectMany(outro => new[] { Domain, Application, Infrastructure, Endpoints }.Select(c => Namespace(outro, c)))
            .ToArray();

        TestResult result = Types.InAssemblies(AssembliesDoModulo(modulo))
            .ShouldNot()
            .HaveDependencyOnAny(proibidos)
            .GetResult();

        ArchAssert.Passou(result, $"O módulo {modulo} acessa Domain, Application, Infrastructure ou Endpoints de outro módulo.");
    }

    [Theory]
    [MemberData(nameof(TodosOsModulos), MemberType = typeof(Solucao))]
    public void Contracts_nao_pode_expor_o_interior_do_modulo(string modulo)
    {
        TestResult result = Types.InAssembly(Assembly(modulo, Contracts))
            .ShouldNot()
            .HaveDependencyOnAny(
                Namespace(modulo, Domain),
                Namespace(modulo, Application),
                Namespace(modulo, Infrastructure),
                Namespace(modulo, Endpoints))
            .GetResult();

        ArchAssert.Passou(result, $"{modulo}.Contracts depende de camadas internas do módulo.");
    }

    [Fact]
    public void Relatorios_nao_tem_entidades_nem_DbContext()
    {
        TestResult entidades = Types.InAssembly(Assembly("Relatorios", Domain))
            .ShouldNot()
            .Inherit(typeof(BuildingBlocks.Domain.Entities.Entity<>))
            .GetResult();

        TestResult dbContexts = Types.InAssembly(Assembly("Relatorios", Infrastructure))
            .ShouldNot()
            .Inherit(typeof(Microsoft.EntityFrameworkCore.DbContext))
            .GetResult();

        ArchAssert.Passou(entidades, "Relatorios é somente leitura e não pode ter entidades (INF-01).");
        ArchAssert.Passou(dbContexts, "Relatorios lê as views do schema relatorios com Dapper e não pode ter DbContext (INF-01).");
    }
}
