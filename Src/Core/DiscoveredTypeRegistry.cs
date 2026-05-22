namespace FastEndpoints;

/// <summary>
/// a global registry that generator-compiled assemblies push their discovered types into via <c>[ModuleInitializer]</c>.
/// consumed by endpoint discovery and messaging setup before falling back to reflection.
/// </summary>
public static class DiscoveredTypeRegistry
{
    static readonly List<Type> _types = [];

    /// <summary>
    /// called from the <c>[ModuleInitializer]</c> emitted by <c>FastEndpoints.Generator</c> in each compiled assembly.
    /// </summary>
    public static void AutoRegister(IEnumerable<Type> types)
        => _types.AddRange(types);

    /// <summary>
    /// override used by tests — clears the registry first, then registers the provided types.
    /// </summary>
    internal static void Override(params Type[] types)
    {
        _types.Clear();
        _types.AddRange(types);
    }

    internal static bool HasTypes => _types.Count > 0;
    internal static IReadOnlyList<Type> All => _types;
}
