using System.Reflection;
using System.Diagnostics.CodeAnalysis;

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

    [UnconditionalSuppressMessage("aot", "IL2067"), UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL2070")]
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
               //the exclusions are plain ascii prefixes, so Ordinal is both correct and free of icu collation.
               .Where(a => !a.IsDynamic && (opts.Assemblies?.Contains(a) is true || !_exclusions.Any(x => a.FullName!.StartsWith(x, StringComparison.Ordinal))))
               .SelectMany(a => a.GetTypes())
               .Where(t => IsTypeMatch(t, opts));

        static bool IsTypeMatch(Type t, AssemblyScanOptions options)
        {
            if (t.IsAbstract || t.IsInterface || t.IsGenericType)
                return false;

            if (options.ExcludeAttribute is not null && t.IsDefined(options.ExcludeAttribute))
                return false;

            if (options.InterfaceTypes.Length > 0 && !ImplementsAny(t, options.InterfaceTypes))
                return false;

            return options.TypeFilter is null || options.TypeFilter(t);
        }

        static bool ImplementsAny(Type t, Type[] interfaceTypes)
        {
            foreach (var tImplemented in t.GetInterfaces())
            {
                foreach (var tWanted in interfaceTypes)
                {
                    if (tImplemented == tWanted)
                        return true;
                }
            }

            return false;
        }
    }
}