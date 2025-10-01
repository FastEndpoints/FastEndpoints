namespace FastEndpoints;

/// <summary>
/// global endpoint versioning options
/// </summary>
public sealed class VersioningOptions
{
    /// <summary>
    /// the prefix used in front of the version (for example 'v' produces 'v{version}').
    /// </summary>
    public string? Prefix { internal get; set; }

    /// <summary>
    /// if a route template is specified here, the template string in endpoint routes will be replaced by the version of the endpoint.
    /// this setting will render <see cref="PrependToRoute" /> ineffective if also set.
    /// setting a value here makes it mandatory to specify the template in all routes of versioned endpoints.
    /// </summary>
    public string? RouteTemplate { get; set; }

    /// <summary>
    /// this value will be used on endpoints that does not specify a version
    /// </summary>
    public int DefaultVersion { internal get; set; }

    /// <summary>
    /// set to true if you'd like to prefix the version to the route instead of being suffixed which is the default.
    /// if <see cref="RouteTemplate" /> is specified, that will take precedence over this setting.
    /// </summary>
    public bool? PrependToRoute { internal get; set; }

    internal bool IsUsingAspVersioning { get; set; }
}