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
    /// this value will be used on endpoints that does not specify a version
    /// </summary>
    public int DefaultVersion { internal get; set; }

    /// <summary>
    /// set to true if you'd like to prefix the version to the route instead of being suffixed which is the default
    /// </summary>
    public bool? PrependToRoute { internal get; set; }

    internal bool IsUsingAspVersioning { get; set; }
}