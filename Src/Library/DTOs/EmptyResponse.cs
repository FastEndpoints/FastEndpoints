namespace FastEndpoints;

/// <summary>
/// a response dto that doesn't have any properties
/// </summary>
public sealed class EmptyResponse
{
    /// <summary>
    /// a cached empty response instance
    /// </summary>
    public static EmptyResponse Instance { get; } = new();

    //private constructor only used by above cached instance.
    EmptyResponse() { }
}