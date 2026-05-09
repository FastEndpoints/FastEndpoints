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
        var resolvedHttpContext = httpContext ?? services.GetService<IHttpContextAccessor>()?.HttpContext ?? new DefaultHttpContext();
        var principal = user ?? resolvedHttpContext.User;

        if (!ReferenceEquals(resolvedHttpContext.User, principal))
            resolvedHttpContext.User = principal;

        return (principal, resolvedHttpContext);
    }
}