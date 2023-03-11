using FluentValidation;
using System.Reflection;

namespace FastEndpoints;

/// <summary>
/// defines how endpoint discovery and registration should be done at startup
/// </summary>
public sealed class EndpointDiscoveryOptions
{
    /// <summary>
    /// an optional collection of assemblies to discover endpoints from.
    /// if DisableAutoDiscovery is set to true, this must be provided.
    /// <para>NOTE: not applicable when using FastEndpoints.Generator package</para>
    /// </summary>
    public IEnumerable<Assembly>? Assemblies { internal get; set; }

    /// <summary>
    /// set to true if only the provided Assemblies should be scanned for endpoints.
    /// if the Assemblies property is null and this is set to true, an exception will be thrown.
    /// <para>NOTE: not applicable when using FastEndpoints.Generator package</para>
    /// </summary>
    public bool DisableAutoDiscovery { internal get; set; }

    /// <summary>
    /// an optional predicate to filter out the final collection of assemblies before scanning for endpoints.
    /// <para>NOTE: not applicable when using FastEndpoints.Generator package</para>
    /// </summary>
    public Func<Assembly, bool>? AssemblyFilter { internal get; set; }

    /// <summary>
    /// if using the FastEndpoints.Generator package, assign <c>DiscoveredTypes.All</c> to this property.
    /// doing so will use the types discovered during source generation instead of reflection based type discovery.
    /// </summary>
    public Type[]? SourceGeneratorDiscoveredTypes { internal get; set; }

    /// <summary>
    /// by default only validators inheriting <see cref="Validator{TRequest}"/> are auto registered.
    /// if you'd like to also include validators inheriting <see cref="AbstractValidator{T}"/>, set this to true.
    /// </summary>
    public bool IncludeAbstractValidators { internal get; set; }

    /// <summary>
    /// a function to filter out types from auto discovery.
    /// the function you set here will be executed for each discovered type during startup.
    /// return 'false' from the function if you want to exclude a type from discovery.
    /// return 'true' to include.
    /// </summary>
    public Func<Type, bool>? Filter { internal get; set; }
}