namespace FastEndpoints;

/// <summary>
/// use this attribute to specify a PATCH route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class HttpPatchAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a PATCH route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpPatchAttribute(params string[] routes) : base(Http.PATCH, routes) { }
}
