namespace FastEndpoints;

/// <summary>
/// a class used for providing a textual description about an endpoint for swagger
/// </summary>
public class EndpointSummary
{
    /// <summary>
    /// the short summary of the endpoint
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// the long description of the endpoint
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// the descriptions of the different responses/ status codes an endpoint can return
    /// </summary>
    public Dictionary<int, string> Responses { get; set; } = new Dictionary<int, string>();

    /// <summary>
    /// indexer for the response descriptions
    /// </summary>
    /// <param name="statusCode">the status code of the response you want to access</param>
    /// <returns>the text description</returns>
    public string this[int statusCode]
    {
        get => Responses[statusCode];
        set => Responses[statusCode] = value;
    }
}
