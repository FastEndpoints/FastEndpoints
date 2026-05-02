using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace FastEndpoints.Mcp;

static class CallerContextResolver
{
    public static (ClaimsPrincipal Principal, HttpContext HttpContext) Resolve(IServiceProvider services,
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

    public static (ClaimsPrincipal Principal, HttpContext HttpContext) Resolve<TParams>(RequestContext<TParams> ctx)
        => Resolve(ctx.Services!, ctx.User);
}
