using System.Reflection;

namespace Funeraria.Modules.Documentos.Infrastructure;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
