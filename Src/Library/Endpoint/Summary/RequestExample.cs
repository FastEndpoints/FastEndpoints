namespace FastEndpoints;

/// <summary>
/// represents a swagger example request analogous to an OpenApiExample
/// </summary>
/// <param name="value">the actual example request object</param>
/// <param name="label">the label/name for this example request</param>
/// <param name="summary">the summary text of this example request</param>
/// <param name="description">the description of this example request</param>
public sealed class RequestExample(object value, string label = "Example", string? summary = null, string? description = null)
{
    /// <summary>
    /// the summary text of this example request
    /// </summary>
    public string? Summary { get; init; } = summary;

    /// <summary>
    /// the description of this example request
    /// </summary>
    public string? Description { get; init; } = description;

    /// <summary>
    /// the actual example request object
    /// </summary>
    public object Value { get; init; } = value;

    /// <summary>
    /// the label/name for this example request
    /// </summary>
    public string Label { get; internal set; } = label;
}