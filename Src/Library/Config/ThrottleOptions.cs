namespace FastEndpoints;

/// <summary>
/// global settings for throttling
/// </summary>
public sealed class ThrottleOptions
{
    /// <summary>
    /// header used to track rate limits
    /// </summary>
    public string? HeaderName { internal get; set; }

    /// <summary>
    /// custom error response message for throttled requests
    /// </summary>
    public string? Message { internal get; set; }
}