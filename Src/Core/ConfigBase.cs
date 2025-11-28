namespace FastEndpoints;

/// <summary>
/// base configuration class providing common service resolver management
/// </summary>
/// <typeparam name="TResolver">the type of service resolver used by this configuration</typeparam>
public abstract class ConfigBase<TResolver> where TResolver : class, IServiceResolverBase
{
    static TResolver? _resolver;

    /// <summary>
    /// Indicates whether the service resolver is not set.
    /// </summary>
    internal static bool ResolverIsNotSet => _resolver is null;

    /// <summary>
    /// Gets the service resolver.
    /// </summary>
    /// <exception cref="InvalidOperationException"></exception>
    internal static TResolver ServiceResolver
    {
        get => _resolver ?? throw new InvalidOperationException("Service resolver is null! Have you done the unit test setup correctly?");
        set => _resolver = value;
    }
}