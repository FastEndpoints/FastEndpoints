namespace FastEndpoints;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public abstract class HttpAttribute : Attribute
{
    internal Http Verb { get; set; }
    internal string[] Routes { get; set; }

    protected HttpAttribute(Http verb, string route)
    {
        Verb = verb;
        Routes = new[] { route };
    }

    protected HttpAttribute(Http verb, params string[] routes)
    {
        Verb = verb;
        Routes = routes;
    }
}

/// <summary>
/// use this attribute to specify a GET route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HttpGetAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a GET route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpGetAttribute(params string[] routes) : base(Http.GET, routes) { }
}

/// <summary>
/// use this attribute to specify a POST route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HttpPostAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a POST route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpPostAttribute(params string[] routes) : base(Http.POST, routes) { }
}

/// <summary>
/// use this attribute to specify a PUT route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HttpPutAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a PUT route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpPutAttribute(params string[] routes) : base(Http.PUT, routes) { }
}

/// <summary>
/// use this attribute to specify a PATCH route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HttpPatchAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a PATCH route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpPatchAttribute(params string[] routes) : base(Http.PATCH, routes) { }
}

/// <summary>
/// use this attribute to specify a DELETE route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class HttpDeleteAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a DELETE route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpDeleteAttribute(params string[] routes) : base(Http.DELETE, routes) { }
}

/// <summary>
/// rate limit requests to this endpoint based on a request http header sent by the client.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class ThrottleAttribute : Attribute
{
    /// <summary>
    /// how many requests are allowed within the given duration
    /// </summary>
    public int HitLimit { get; set; }

    /// <summary>
    /// the frequency in seconds where the accrued hit count should be reset
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// the name of the request header used to uniquely identify clients.
    /// header name can also be configured globally using <c>app.UseFastEndpoints(c=> c.Throttle...)</c>
    /// not specifying a header name will first look for 'X-Forwarded-For' header and if not present, will use `HttpContext.Connection.RemoteIpAddress`.
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// rate limit requests to this endpoint based on a request http header sent by the client.
    /// </summary>
    /// <param name="hitLimit">how many requests are allowed within the given duration</param>
    /// <param name="durationSeconds">the frequency in seconds where the accrued hit count should be reset</param>
    /// <param name="headerName">
    /// the name of the request header used to uniquely identify clients.
    /// header name can also be configured globally using <c>app.UseFastEndpoints(c=> c.ThrottleOptions...)</c>
    /// not specifying a header name will first look for 'X-Forwarded-For' header and if not present, will use `HttpContext.Connection.RemoteIpAddress`.
    /// </param>
    public ThrottleAttribute(int hitLimit, double durationSeconds, string? headerName = null)
    {
        HitLimit = hitLimit;
        DurationSeconds = durationSeconds;
        HeaderName = headerName;
    }
}

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the incoming request's json body.
/// <para>HINT: no other binding sources will be used for binding that property.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FromBodyAttribute : Attribute { }

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FromClaimAttribute : Attribute
{
    /// <summary>
    /// the claim type to auto bind
    /// </summary>
    public string? ClaimType { get; set; }

    /// <summary>
    /// set to true if a validation error should be thrown when the current user principal doesn't have the specified claim
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
    /// </summary>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current user principal doesn't have a claim type matching the property name being bound to.</param>
    public FromClaimAttribute(bool isRequired)
    {
        ClaimType = null;
        IsRequired = isRequired;
    }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal
    /// </summary>
    /// <param name="claimType">optionally specify the claim type to bind from. if not specified, the claim type of the user principal must match the name of the property being bound to.</param>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current user principal doesn't have the specified claim type</param>
    public FromClaimAttribute(string? claimType = null, bool isRequired = true)
    {
        ClaimType = claimType;
        IsRequired = isRequired;
    }
}

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal.
/// this is a shorter alias for the [FromClaim] attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FromAttribute : FromClaimAttribute
{
    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant claim of the current user principal.
    /// this is a shorter alias for the [FromClaim] attribute.
    /// </summary>
    /// <param name="claimType">the claim type to auto bind</param>
    /// <param name="isRequired">set to true if a validation error should be thrown when the current user principal doesn't have the specified claim</param>
    public FromAttribute(string claimType, bool isRequired = true) : base(claimType, isRequired) { }
}

/// <summary>
/// properties decorated with this attribute will have their values auto bound from the relevant http header of the current request.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class FromHeaderAttribute : Attribute
{
    /// <summary>
    /// the header name to auto bind from
    /// </summary>
    public string? HeaderName { get; set; }

    /// <summary>
    /// set to true if a validation error should be thrown when the current request doesn't have the specified header
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant http header of the current request.
    /// </summary>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current user request doesn't have a header matching the property name being bound to.</param>
    public FromHeaderAttribute(bool isRequired)
    {
        HeaderName = null;
        IsRequired = isRequired;
    }

    /// <summary>
    /// properties decorated with this attribute will have their values auto bound from the relevant http header of the current request.
    /// </summary>
    /// <param name="headerName">optionally specify the header name to bind from. if not specified, the header name must match the name of the property being bound to.</param>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current request doesn't have the specified header.</param>
    public FromHeaderAttribute(string? headerName = null, bool isRequired = true)
    {
        HeaderName = headerName;
        IsRequired = isRequired;
    }
}

/// <summary>
/// boolean properties decorated with this attribute will have their values set to true if the current principal has the specified permission.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class HasPermissionAttribute : Attribute
{
    /// <summary>
    /// the permission to check for
    /// </summary>
    public string Permission { get; set; }

    /// <summary>
    /// set to true if a validation error should be thrown when the current user principal doesn't have the specified permission
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// boolean properties decorated with this attribute will have their values set to true if the current principal has the specified permission.
    /// </summary>
    /// <param name="permission">the permission to check for</param>
    /// <param name="isRequired">set to false if a validation error shouldn't be thrown when the current principal doesn't have the specified permission.</param>
    public HasPermissionAttribute(string permission, bool isRequired = true)
    {
        Permission = permission;
        IsRequired = isRequired;
    }
}

/// <summary>
/// use this attribute to specify the name of route param, query param, or form field if it's different from the name of the property being bound to.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class BindFromAttribute : Attribute
{
    /// <summary>
    /// the name of the incoming query param, route param or form field
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// use this attribute to specify the name of route param, query param, or form field if it's different from the name of the property being bound to.
    /// </summary>
    /// <param name="name">the name to use for binding</param>
    public BindFromAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// properties decorated with this attribute will have a corresponding request parameter added to the swagger schema
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public class QueryParamAttribute : Attribute { }

/// <summary>
/// attribute used to mark classes that should be hidden from public api
/// </summary>
[AttributeUsage(AttributeTargets.All, AllowMultiple = false), HideFromDocs]
public class HideFromDocsAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
internal sealed class NotImplementedAttribute : Attribute { }