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
    public string DefaultVersion { get; set; } = "1";
}