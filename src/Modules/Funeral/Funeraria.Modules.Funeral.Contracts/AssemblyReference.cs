using System.Reflection;

namespace Funeraria.Modules.Funeral.Contracts;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
