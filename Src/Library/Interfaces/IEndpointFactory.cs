using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// interface for the creation of endpoints.
/// </summary>
public interface IEndpointFactory
{
    /// <summary>
    /// returns the instantiated fast endpoint from a given <see cref="EndpointDefinition"/> and <see cref="HttpContext"/>
    /// </summary>
    /// <param name="definition">the endpoint definition for the current request</param>
    /// <param name="ctx">the http context of the current request</param>
    BaseEndpoint Create(EndpointDefinition definition, HttpContext ctx);
}