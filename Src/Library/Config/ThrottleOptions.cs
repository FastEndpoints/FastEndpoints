namespace FastEndpoints;

/// <summary>
/// global settings for throttling
/// </summary>
public class ThrottleOptions
{
    /// <summary>
    /// header used to track rate limits
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// custom error response for throttled requests
    /// </summary>
    public string? ThrottledResponse { get; set; }
}