namespace FastEndpoints;

/// <summary>
/// implement this interface on your request dto if you need to model bind the raw content body of an incoming http request
/// </summary>
public interface IPlainTextRequest
{
    /// <summary>
    /// the request body content will be bound to this property
    /// </summary>
    string Content { get; set; }
}
