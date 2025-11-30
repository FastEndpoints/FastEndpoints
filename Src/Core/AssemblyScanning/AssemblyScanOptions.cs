using System.Reflection;

namespace FastEndpoints;

/// <summary>
/// options for configuring assembly scanning behavior.
/// </summary>
public sealed class AssemblyScanOptions
{
    /// <summary>
    /// whether to disable automatic discovery of assemblies from the current app domain.
    /// if set to true, assemblies must be provided via <see cref="Assemblies" />.
    /// </summary>
    public bool DisableAutoDiscovery { get; set; }

    /// <summary>
    /// a collection of assemblies to scan for types.
    /// when provided, these assemblies are included in addition to auto-discovered assemblies
    /// (unless <see cref="DisableAutoDiscovery" /> is true).
    /// </summary>
    public IEnumerable<Assembly>? Assemblies { get; set; }

    /// <summary>
    /// an optional filter to select which assemblies should be scanned.
    /// if provided, only assemblies for which this predicate returns true will be scanned.
    /// </summary>
    public Func<Assembly, bool>? AssemblyFilter { get; set; }

    /// <summary>
    /// an optional filter to select which types should be included in the results.
    /// if provided, only types for which this predicate returns true will be returned.
    /// </summary>
    public Func<Type, bool>? TypeFilter { get; set; }

    /// <summary>
    /// the interfaces that discovered types must implement at least one of.
    /// only non-abstract, non-interface, non-generic types implementing at least one of these interfaces will be returned.
    /// </summary>
    public Type[] InterfaceTypes { get; set; } = [];

    /// <summary>
    /// an optional attribute type. if specified, types decorated with this attribute will be excluded.
    /// </summary>
    public Type? ExcludeAttribute { get; set; }
}
