using JetBrains.Annotations;

namespace FastEndpoints;

/// <summary>
/// use this attribute to specify a QUERY route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HttpQueryAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a QUERY route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpQueryAttribute([RouteTemplate] params string[] routes) : base(Http.QUERY, routes) { }
}