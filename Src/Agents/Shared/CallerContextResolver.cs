using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Agents;

static class CallerContextResolver
{
    internal static (ClaimsPrincipal Principal, HttpContext HttpContext) Resolve(IServiceProvider services,
                                                                                ClaimsPrincipal? user = null,
                                                                                HttpContext? httpContext = null)
    {
        var principal = user ?? httpContext?.User ?? new ClaimsPrincipal();
        var resolvedHttpContext = httpContext ?? services.GetService<IHttpContextAccessor>()?.HttpContext ?? new DefaultHttpContext();

        if (!ReferenceEquals(resolvedHttpContext.User, principal))
            resolvedHttpContext.User = principal;

        if (resolvedHttpContext.RequestServices is null)
            resolvedHttpContext.RequestServices = services;

        return (principal, resolvedHttpContext);
    }
}
