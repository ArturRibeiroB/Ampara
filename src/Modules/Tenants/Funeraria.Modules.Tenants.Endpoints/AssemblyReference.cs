using System.Reflection;

namespace Funeraria.Modules.Tenants.Endpoints;

public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
