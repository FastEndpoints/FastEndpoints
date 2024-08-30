namespace FastEndpoints.Swagger;

/// <summary>
/// represents an auto-tag override value.
/// </summary>
/// <param name="tagName"></param>
public sealed class AutoTagOverride(string tagName)
{
    public string TagName { get; set; } = tagName;
}