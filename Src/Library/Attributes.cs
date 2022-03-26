namespace FastEndpoints;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public abstract class HttpAttribute : Attribute
{
    internal Http Verb { get; set; }
    internal string Route { get; set; }

    protected HttpAttribute(Http verb, string route)
    {
        Verb = verb;
        Route = route;
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
    /// <param name="route">the route for the endpoint</param>
    public HttpGetAttribute(string route) : base(Http.GET, route) { }
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
    /// <param name="route">the route for the endpoint</param>
    public HttpPostAttribute(string route) : base(Http.POST, route) { }
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
    /// <param name="route">the route for the endpoint</param>
    public HttpPutAttribute(string route) : base(Http.PUT, route) { }
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
    /// <param name="route">the route for the endpoint</param>
    public HttpPatchAttribute(string route) : base(Http.PATCH, route) { }
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
    /// <param name="route">the route for the endpoint</param>
    public HttpDeleteAttribute(string route) : base(Http.DELETE, route) { }
}

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