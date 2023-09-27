namespace FastEndpoints;

/// <summary>
/// describes a swagger response header for a certain response dto
/// </summary>
public sealed class ResponseHeader
{
    /// <summary>
    /// specify which http status code this response header applies to
    /// </summary>
    internal int StatusCode { get; init; }

    /// <summary>
    /// the name of the header
    /// </summary>
    internal string HeaderName { get; init; }

    /// <summary>
    /// description for the header
    /// </summary>
    public string? Description { internal get; set; }

    /// <summary>
    /// an example header value
    /// </summary>
    public object? Example { internal get; set; }

    public ResponseHeader(int statusCode, string headerName)
    {
        StatusCode = statusCode;
        HeaderName = headerName;
    }
}