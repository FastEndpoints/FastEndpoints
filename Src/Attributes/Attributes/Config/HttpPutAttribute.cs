using JetBrains.Annotations;

namespace FastEndpoints;

/// <summary>
/// use this attribute to specify a PUT route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HttpPutAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a PUT route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpPutAttribute([RouteTemplate] params string[] routes) : base(Http.PUT, routes) { }
}