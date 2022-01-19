namespace FastEndpoints;

/// <summary>
/// Endpoint versioning options.
/// </summary>
public class VersioningOptions
{
	/// <summary>
	/// the prefix used in front of the version (for example 'v' produces 'v{version}').
	/// </summary>
	public string Prefix { get; set; }

	/// <summary>
	/// when the endpoint has no version value set, use this value as a default value.
	/// </summary>
	public string DefaultVersion { get; set; }
}