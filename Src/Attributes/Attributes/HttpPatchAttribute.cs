using JetBrains.Annotations;
#if NET7_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace FastEndpoints;

/// <summary>
/// use this attribute to specify a PATCH route for an endpoint
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class HttpPatchAttribute : HttpAttribute
{
    /// <summary>
    /// use this attribute to specify a PATCH route for an endpoint
    /// </summary>
    /// <param name="routes">the routes for the endpoint</param>
    public HttpPatchAttribute(
    #if NET7_0_OR_GREATER
        [StringSyntax("Route")]
    #endif
        [RouteTemplate] params string[] routes) : base(Http.PATCH, routes) { }
}