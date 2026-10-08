using System.Reflection;

namespace Funeraria.Modules.Documentos.Contracts;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
