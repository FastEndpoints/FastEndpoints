global using Conf = FastEndpoints.Config;

#pragma warning disable RCS1102,CA1822

namespace FastEndpoints;

/// <summary>
/// global configuration settings for FastEndpoints
/// </summary>
public sealed class Config
{
    static IServiceResolver? _resolver;
    internal static bool ResolverIsNotSet => _resolver is null;

    internal static IServiceResolver ServiceResolver
    {
        get => _resolver ?? throw new InvalidOperationException("Service resolver is null! Have you done the unit test setup correctly?");
        set => _resolver = value;
    }

    internal static BindingOptions BndOpts = new();
    internal static EndpointOptions EpOpts = new();
    internal static ErrorOptions ErrOpts = new();
    internal static SecurityOptions SecOpts = new();
    internal static SerializerOptions SerOpts = new();
    internal static ThrottleOptions ThrOpts = new();
    internal static VersioningOptions VerOpts = new();

    /// <summary>
    /// request binding settings
    /// </summary>
    public BindingOptions Binding => BndOpts;

    /// <summary>
    /// endpoint discovery &amp; registration settings
    /// </summary>
    public EndpointOptions Endpoints => EpOpts;

    /// <summary>
    /// error response customization settings
    /// </summary>
    public ErrorOptions Errors => ErrOpts;

    /// <summary>
    /// security related settings
    /// </summary>
    public SecurityOptions Security => SecOpts;

    /// <summary>
    /// settings for customizing serialization behavior
    /// </summary>
    public SerializerOptions Serializer => SerOpts;

    /// <summary>
    /// endpoint throttling/ rate limiting settings
    /// </summary>
    public ThrottleOptions Throttle => ThrOpts;

    /// <summary>
    /// endpoint versioning settings
    /// </summary>
    public VersioningOptions Versioning => VerOpts;
}