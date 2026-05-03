using FastEndpoints.Agents;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace FastEndpoints.Mcp;

static class CallerContextResolver
{
    public static (ClaimsPrincipal Principal, HttpContext HttpContext) Resolve(IServiceProvider services,
                                                                               ClaimsPrincipal? user = null,
                                                                               HttpContext? httpContext = null)
        => FastEndpoints.Agents.CallerContextResolver.Resolve(services, user, httpContext);

    public static (ClaimsPrincipal Principal, HttpContext HttpContext) Resolve<TParams>(RequestContext<TParams> ctx)
        => FastEndpoints.Agents.CallerContextResolver.Resolve(ctx.Services!, ctx.User);
}
