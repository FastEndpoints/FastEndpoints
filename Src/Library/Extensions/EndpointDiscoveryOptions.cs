using System.Reflection;

namespace FastEndpoints;

/// <summary>
/// defines how endpoint discovery and registration should be done at startup
/// </summary>
public class EndpointDiscoveryOptions
{
    /// <summary>
    /// an optional collection of assemblies to discover endpoints from.
    /// if DisableAutoDiscovery is set to true, this must be provided.
    /// </summary>
    public IEnumerable<Assembly>? Assemblies { get; set; }

    /// <summary>
    /// set to true if only the provided Assemblies should be scanned for endpoints.
    /// if the Assemblies property is null and this is set to true, an exception will be thrown.
    /// </summary>
    public bool DisableAutoDiscovery { get; set; }

    /// <summary>
    /// an optional predicate to filter out the final collection of assemblies before scanning for endpoints.
    /// </summary>
    public Func<Assembly, bool>? AssemblyFilter { get; set; }
}