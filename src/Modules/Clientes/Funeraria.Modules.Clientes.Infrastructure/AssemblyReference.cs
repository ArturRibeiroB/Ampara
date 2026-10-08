using System.Reflection;

namespace Funeraria.Modules.Clientes.Infrastructure;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
