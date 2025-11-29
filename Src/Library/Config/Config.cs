global using Cfg = FastEndpoints.Config;

// ReSharper disable MemberCanBeMadeStatic.Global
#pragma warning disable RCS1102,CA1822

namespace FastEndpoints;

/// <summary>
/// global configuration settings for FastEndpoints
/// </summary>
public sealed class Config
{
    internal static readonly BindingOptions BndOpts = new();
    internal static readonly EndpointOptions EpOpts = new();
    internal static readonly ErrorOptions ErrOpts = new();
    internal static readonly SecurityOptions SecOpts = new();
    internal static readonly SerializerOptions SerOpts = new();
    internal static readonly ThrottleOptions ThrOpts = new();
    internal static readonly VersioningOptions VerOpts = new();
    internal static readonly ValidationOptions ValOpts = new();

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

    /// <summary>
    /// endpoint validation settings
    /// </summary>
    public ValidationOptions Validation => ValOpts;
}