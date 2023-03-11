namespace FastEndpoints;

/// <summary>
/// rate limit requests to this endpoint based on a request http header sent by the client.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ThrottleAttribute : Attribute
{
    /// <summary>
    /// how many requests are allowed within the given duration
    /// </summary>
    public int HitLimit { get; set; }

    /// <summary>
    /// the frequency in seconds where the accrued hit count should be reset
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// the name of the request header used to uniquely identify clients.
    /// header name can also be configured globally using <c>app.UseFastEndpoints(c=> c.Throttle...)</c>
    /// not specifying a header name will first look for 'X-Forwarded-For' header and if not present, will use `HttpContext.Connection.RemoteIpAddress`.
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// rate limit requests to this endpoint based on a request http header sent by the client.
    /// </summary>
    /// <param name="hitLimit">how many requests are allowed within the given duration</param>
    /// <param name="durationSeconds">the frequency in seconds where the accrued hit count should be reset</param>
    /// <param name="headerName">
    /// the name of the request header used to uniquely identify clients.
    /// header name can also be configured globally using <c>app.UseFastEndpoints(c=> c.ThrottleOptions...)</c>
    /// not specifying a header name will first look for 'X-Forwarded-For' header and if not present, will use `HttpContext.Connection.RemoteIpAddress`.
    /// </param>
    public ThrottleAttribute(int hitLimit, double durationSeconds, string? headerName = null)
    {
        HitLimit = hitLimit;
        DurationSeconds = durationSeconds;
        HeaderName = headerName;
    }
}
