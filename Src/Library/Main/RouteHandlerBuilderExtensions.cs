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
    /// clears any number of given produces response type metadata from the endpoint by supplying the status codes of the responses to remove
    /// </summary>
    /// <param name="statusCodes">one or more status codes of the defaults to remove</param>
    public static RouteHandlerBuilder ClearDefaultProduces(this RouteHandlerBuilder hb, params int[] statusCodes)
    {
        hb.Add(epBuilder =>
        {
            foreach (var m in epBuilder.Metadata.ToArray())
            {
                if (m is IProducesResponseTypeMetadata meta && statusCodes.Contains(meta.StatusCode))
                    epBuilder.Metadata.Remove(m);
            }
        });
        return hb;
    }
}
