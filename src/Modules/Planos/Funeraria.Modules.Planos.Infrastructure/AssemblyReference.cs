using System.Reflection;

namespace Funeraria.Modules.Planos.Infrastructure;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
