using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace Funeraria.ArchitectureTests;

internal static class ArchAssert
{
    public static void Passou(TestResult result, string regra)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        string tipos = string.Join(Environment.NewLine, (result.FailingTypeNames ?? []).Select(t => "  - " + t));
        Assert.Fail($"{regra}{Environment.NewLine}Tipos que quebram a regra:{Environment.NewLine}{tipos}");
    }
}
