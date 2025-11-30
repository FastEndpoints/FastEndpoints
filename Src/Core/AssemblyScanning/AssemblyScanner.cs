using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FastEndpoints;

/// <summary>
/// utility for scanning assemblies to discover types based on configurable options.
/// </summary>
public static class AssemblyScanner
{
    /// <summary>
    /// default assembly name prefixes to exclude from scanning.
    /// these are typically framework, third-party libraries that don't contain user types.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultExclusions =
    [
        "Accessibility",
        "FastEndpoints",
        "FluentValidation",
        "Grpc",
        "JetBrains",
        "Microsoft",
        "mscorlib",
        "Namotion",
        "netstandard",
        "Newtonsoft",
        "NJsonSchema",
        "NSwag",
        "NuGet",
        "PresentationCore",
        "PresentationFramework",
        "StackExchange",
        "System",
        "testhost",
        "WindowsBase",
        "YamlDotNet"
    ];

    /// <summary>
    /// scans assemblies and returns all types matching the configured criteria.
    /// </summary>
    /// <param name="options">options controlling which assemblies to scan and which types to return.</param>
    /// <param name="exclusions">
    /// assembly name prefixes to exclude. if null, uses <see cref="DefaultExclusions" />.
    /// if you want no exclusions, pass an empty collection.
    /// </param>
    /// <returns>an enumerable of discovered types.</returns>
    public static IEnumerable<Type> ScanForTypes(AssemblyScanOptions options, IEnumerable<string>? exclusions = null)
    {
        if (options.DisableAutoDiscovery && !(options.Assemblies?.Any() ?? false))
            throw new InvalidOperationException($"If '{nameof(options.DisableAutoDiscovery)}' is true, a collection of assemblies must be provided!");

        exclusions ??= DefaultExclusions;

        var assemblies = GetAssemblies(options, exclusions);

        return assemblies
            .Where(a => !a.IsDynamic && (options.Assemblies?.Contains(a) ?? false || !exclusions.Any(x => a.FullName!.StartsWith(x, StringComparison.Ordinal))))
            .SelectMany(SafeGetTypes)
            .Where(t => IsTypeMatch(t, options));
    }

    static IEnumerable<Assembly> GetAssemblies(AssemblyScanOptions options, IEnumerable<string> _)
    {
        var assemblies = Enumerable.Empty<Assembly>();

        if (options.Assemblies?.Any() ?? false)
            assemblies = options.Assemblies;

        if (!options.DisableAutoDiscovery)
            assemblies = assemblies.Union(AppDomain.CurrentDomain.GetAssemblies());

        if (options.AssemblyFilter is not null)
            assemblies = assemblies.Where(options.AssemblyFilter);

        return assemblies;
    }

    static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Return the types that could be loaded
            return ex.Types.Where(t => t is not null)!;
        }
    }

    static bool IsTypeMatch(Type t, AssemblyScanOptions options)
    {
        // Basic type filter: non-abstract, non-interface, non-generic
        if (t.IsAbstract || t.IsInterface || t.IsGenericType)
            return false;

        // Check exclude attribute if specified
        if (options.ExcludeAttribute is not null && t.IsDefined(options.ExcludeAttribute))
            return false;

        // Check interface requirements
        if (options.InterfaceTypes.Length > 0)
        {
            var typeInterfaces = t.GetInterfaces();
            if (!typeInterfaces.Intersect(options.InterfaceTypes).Any())
                return false;
        }

        // Apply custom type filter
        if (options.TypeFilter is not null && !options.TypeFilter(t))
            return false;

        return true;
    }
}
