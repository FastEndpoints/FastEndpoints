using System.Reflection;

namespace FastEndpoints;

internal static class AssemblyScanner
{
    static readonly IReadOnlyList<string> _exclusions =
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

    internal static IEnumerable<Type> ScanForTypes(AssemblyScanOptions opts)
    {
        if (opts.DisableAutoDiscovery && opts.Assemblies?.Any() is false)
            throw new InvalidOperationException($"If '{nameof(opts.DisableAutoDiscovery)}' is true, a collection of assemblies must be provided!");

        var assemblies = Enumerable.Empty<Assembly>();

        if (opts.Assemblies?.Any() is true)
            assemblies = opts.Assemblies;

        if (!opts.DisableAutoDiscovery)
            assemblies = assemblies.Union(AppDomain.CurrentDomain.GetAssemblies());

        if (opts.AssemblyFilter is not null)
            assemblies = assemblies.Where(opts.AssemblyFilter);

        return assemblies
               .Where(a => !a.IsDynamic && (opts.Assemblies?.Contains(a) is true || !_exclusions.Any(x => a.FullName!.StartsWith(x))))
               .SelectMany(a => a.GetTypes())
               .Where(t => IsTypeMatch(t, opts));

        static bool IsTypeMatch(Type t, AssemblyScanOptions options)
        {
            if (t.IsAbstract || t.IsInterface || t.IsGenericType)
                return false;

            if (options.ExcludeAttribute is not null && t.IsDefined(options.ExcludeAttribute))
                return false;

            return options.InterfaceTypes.Length switch
            {
                > 0 when !t.GetInterfaces().Intersect(options.InterfaceTypes).Any() => false,
                _ => options.TypeFilter is null || options.TypeFilter(t)
            };
        }
    }
}