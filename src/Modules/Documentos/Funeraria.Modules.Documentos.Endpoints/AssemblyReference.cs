using System.Reflection;

namespace Funeraria.Modules.Documentos.Endpoints;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
