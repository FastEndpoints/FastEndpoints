using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// a class used for providing a textual description about an endpoint for swagger
/// </summary>
public class EndpointSummary
{
    internal List<IProducesResponseTypeMetadata> ProducesMetas { get; } = new();

    internal static readonly Action<RouteHandlerBuilder> ClearDefaultProduces200Metadata = b =>
    {
        b.Add(epBuilder =>
        {
            foreach (var m in epBuilder.Metadata.Where(o => o.GetType().Name == Constants.ProducesMetadata).ToArray())
            {
                if (((IProducesResponseTypeMetadata)m).StatusCode == 200)
                    epBuilder.Metadata.Remove(m);
            }
        });
    };

    /// <summary>
    /// the short summary of the endpoint
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// the long description of the endpoint
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// an example request object to be used in swagger/ openapi.
    /// </summary>
    public object? ExampleRequest { get; set; }

    /// <summary>
    /// the descriptions of the different responses/ status codes an endpoint can return
    /// </summary>
    public Dictionary<int, string> Responses { get; set; } = new();

    /// <summary>
    /// the response examples for each status code
    /// </summary>
    public Dictionary<int, object> ResponseExamples { get; set; } = new();

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
    public string this[int statusCode] {
        get => Responses[statusCode];
        set => Responses[statusCode] = value;
    }

    /// <summary>
    /// add a response description to the swagger document
    /// </summary>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="statusCode">http status code</param>
    /// <param name="description">the description of the response</param>
    /// <param name="contentType">the media/content type of the response</param>
    /// <param name="example">and example response dto instance</param>
    public void Response<TResponse>(int statusCode = 200, string? description = null, string contentType = "application/json", TResponse? example = default)
    {
        ProducesMetas.Add(new ProducesResponseTypeMetadata
        {
            ContentTypes = new[] { contentType },
            StatusCode = statusCode,
            Type = typeof(TResponse),
            Example = example
        });

        if (description is not null)
            Responses[statusCode] = description;
    }

    /// <summary>
    /// add a response description that doesn't have a response dto to the swagger document
    /// </summary>
    /// <param name="statusCode">http status code</param>
    /// <param name="description">the description of the response</param>
    /// <param name="contentType">the media/content type of the response</param>
    public void Response(int statusCode = 200, string? description = null, string? contentType = null)
    {
        ProducesMetas.Add(new ProducesResponseTypeMetadata
        {
            ContentTypes = contentType is null ? Enumerable.Empty<string>() : new[] { contentType },
            StatusCode = statusCode,
            Type = typeof(void)
        });

        if (description is not null)
            Responses[statusCode] = description;
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

///<inheritdoc/>
///<typeparam name="TEndpoint">the type of the endpoint this summary is associated with</typeparam>
public abstract class Summary<TEndpoint> : EndpointSummary, ISummary where TEndpoint : IEndpoint { }

///<inheritdoc/>
///<typeparam name="TEndpoint">the type of the endpoint this summary is associated with</typeparam>
///<typeparam name="TRequest">the type of the request dto</typeparam>
public abstract class Summary<TEndpoint, TRequest> : EndpointSummary<TRequest>, ISummary where TEndpoint : IEndpoint where TRequest : new() { }

internal class ProducesResponseTypeMetadata : IProducesResponseTypeMetadata
{
    public Type? Type { get; set; }

    public int StatusCode { get; set; }

    public IEnumerable<string> ContentTypes { get; set; }

    public object? Example { get; set; }
}