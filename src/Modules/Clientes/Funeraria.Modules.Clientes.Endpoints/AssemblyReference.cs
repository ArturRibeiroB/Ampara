using System.Reflection;

namespace Funeraria.Modules.Clientes.Endpoints;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
