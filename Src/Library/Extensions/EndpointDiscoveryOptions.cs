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
    /// <para>NOTE: not applicable when using FastEndpoints.Generator package</para>
    /// </summary>
    public IEnumerable<Assembly>? Assemblies { get; set; }

    /// <summary>
    /// set to true if only the provided Assemblies should be scanned for endpoints.
    /// if the Assemblies property is null and this is set to true, an exception will be thrown.
    /// <para>NOTE: not applicable when using FastEndpoints.Generator package</para>
    /// </summary>
    public bool DisableAutoDiscovery { get; set; }

    /// <summary>
    /// an optional predicate to filter out the final collection of assemblies before scanning for endpoints.
    /// <para>NOTE: not applicable when using FastEndpoints.Generator package</para>
    /// </summary>
    public Func<Assembly, bool>? AssemblyFilter { get; set; }

    /// <summary>
    /// if using the FastEndpoints.Generator package, assign <c>DiscoveredTypes.All</c> to this property.
    /// doing so will use the types discovered during source generation instead of reflection based type discovery.
    /// </summary>
    public Type[]? SourceGeneratorDiscoveredTypes { get; set; }
    
    /// <summary>
    /// an optional list to allow the definition of what types should be handled as a validator.
    /// if no types are defined here only validators that inherit from <see cref="Validator{TRequest}"/> are registered.
    /// </summary>
    public Type[]? CustomValidatorTypes { get; set; }
}