namespace FastEndpoints;

/// <summary>
/// use this dto if you need to model bind the raw content body of an incoming http request or you may implement the IPlainTextRequest interface on your own request dto.
/// </summary>
public class PlainTextRequest : IPlainTextRequest
{
    /// <summary>
    /// the body content of the incoming request
    /// </summary>
    public string Content { get; set; }
}