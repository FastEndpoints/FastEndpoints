namespace FastEndpoints;

/// <summary>
/// use this attribute to specify a GET route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HttpGetAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a GET route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpGetAttribute(params string[] routes) : base(Http.GET, routes) { }
}
