using System.Linq.Expressions;

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
    public Dictionary<int, string> Responses { get; set; } = new();

    /// <summary>
    /// the descriptions for endpoint paramaters. you can add descriptions for route/query params and request dto properties.
    /// what you specify here will take precedence over xml comments of dto classes (if they are also specified).
    /// </summary>
    public Dictionary<string, string> Params { get; set; } = new();

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

///<inheritdoc/>
///<typeparam name="TRequest">the type of the request dto</typeparam>
public class EndpointSummary<TRequest> : EndpointSummary where TRequest : new()
{
    /// <summary>
    /// add a description for a request param for a given property of the request dto
    /// </summary>
    /// <param name="property">a member expression for specifying which property the description is for</param>
    /// <param name="description">the description text</param>
    public void RequestParam(Expression<Func<TRequest, object>> property, string description)
        => Params[property.PropertyName()] = description;
}