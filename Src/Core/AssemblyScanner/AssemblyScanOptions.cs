using System.Reflection;

namespace FastEndpoints;

internal sealed class AssemblyScanOptions
{
    public bool DisableAutoDiscovery { get; set; }
    public IEnumerable<Assembly>? Assemblies { get; set; }
    public Func<Assembly, bool>? AssemblyFilter { get; set; }
    public Func<Type, bool>? TypeFilter { get; set; }
    public Type[] InterfaceTypes { get; set; } = [];
    public Type? ExcludeAttribute { get; set; }
}