namespace FastEndpoints;

/// <summary>
/// use this attribute to specify a POST route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HttpPostAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a POST route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpPostAttribute(params string[] routes) : base(Http.POST, routes) { }
}
