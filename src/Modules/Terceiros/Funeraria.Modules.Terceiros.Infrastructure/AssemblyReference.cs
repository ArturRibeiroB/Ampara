using System.Reflection;

namespace Funeraria.Modules.Terceiros.Infrastructure;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
