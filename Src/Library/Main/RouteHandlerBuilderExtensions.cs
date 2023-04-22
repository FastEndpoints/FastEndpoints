using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;

namespace FastEndpoints;

public static class RouteHandlerBuilderExtensions
{
    /// <summary>
    /// adds produces metadata for a given type of error response dto
    /// </summary>
    /// <typeparam name="TResponse">the type of the error response</typeparam>
    /// <param name="statusCode">the status code of the error response</param>
    /// <param name="contentType">content type header value</param>
    public static RouteHandlerBuilder ProducesProblemFE<TResponse>(this RouteHandlerBuilder hb, int statusCode = 400, string contentType = "application/problem+json")
        => hb.Produces<TResponse>(statusCode, contentType);

    /// <summary>
    /// adds produces metadata of type <see cref="ErrorResponse"/> to the endpoint description
    /// </summary>
    /// <param name="statusCode">the status code of the error response</param>
    /// <param name="contentType">content type header value</param>
    public static RouteHandlerBuilder ProducesProblemFE(this RouteHandlerBuilder hb, int statusCode = 400, string contentType = "application/problem+json")
         => hb.ProducesProblemFE<ErrorResponse>(statusCode, contentType);

    /// <summary>
    /// adds produces metadata of type <see cref="ProblemDetails"/> (RFC7807 compatible) to the endpoint description
    /// </summary>
    /// <param name="statusCode">the status code of the error response</param>
    /// <param name="contentType">content type header value</param>
    public static RouteHandlerBuilder ProducesProblemDetails(this RouteHandlerBuilder hb, int statusCode = 400, string contentType = "application/problem+json")
        => hb.ProducesProblemFE<ProblemDetails>(statusCode, contentType);

    /// <summary>
    /// clears just the default "accepts metadata" from the endpoint.
    /// </summary>
    public static RouteHandlerBuilder ClearDefaultAccepts(this RouteHandlerBuilder hb)
    {
        hb.Add(epBuilder =>
        {
            var metaData = epBuilder.Metadata.ToArray(); //need to make a copy since we're modifying the list
            for (var i = 0; i < metaData.Length; i++)
            {
                if (metaData[i] is IAcceptsMetadata)
                    epBuilder.Metadata.Remove(metaData[i]);
            }
        });
        return hb;
    }

    /// <summary>
    /// clears any number of given "produces metadata" from the endpoint by supplying the status codes of the responses to remove.
    /// not specifying any status codes will result in all produces metadata being removed.
    /// </summary>
    /// <param name="statusCodes">one or more status codes of the defaults to remove</param>
    public static RouteHandlerBuilder ClearDefaultProduces(this RouteHandlerBuilder hb, params int[] statusCodes)
    {
        hb.Add(epBuilder =>
        {
            var metaData = epBuilder.Metadata.ToArray(); //need to make a copy since we're modifying the list
            for (var i = 0; i < metaData.Length; i++)
            {
                if (metaData[i] is IProducesResponseTypeMetadata meta && (statusCodes.Length == 0 || statusCodes.Contains(meta.StatusCode)))
                    epBuilder.Metadata.Remove(metaData[i]);
            }
        });
        return hb;
    }
}
