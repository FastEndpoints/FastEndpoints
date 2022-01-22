namespace FastEndpoints;

/// <summary>
/// global endpoint versioning options
/// </summary>
public class VersioningOptions
{
    /// <summary>
    /// the prefix used in front of the version (for example 'v' produces 'v{version}').
    /// </summary>
    public string Prefix { get; set; } = "v";

    /// <summary>
    /// this value will be used on endpoints that does not specify a version
    /// </summary>
    public int DefaultVersion { get; set; } = 0;

    /// <summary>
    /// appends the version to the route at the end instead being prefixed.
    /// </summary>
    public bool SuffixedVersion { get; set; }
}