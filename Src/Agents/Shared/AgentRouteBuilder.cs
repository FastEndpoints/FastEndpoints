using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace FastEndpoints.Agents;

static class AgentRouteBuilder
{
    internal static IEndpointRouteBuilder RequireEndpointRouteBuilder(IApplicationBuilder app, string methodName)
    {
        if (app is IEndpointRouteBuilder routes)
            return routes;

        throw new InvalidOperationException(
            $"{methodName} must be called on an IApplicationBuilder that also implements IEndpointRouteBuilder (such as WebApplication). " +
            $"Call {methodName} after building the WebApplication, or after UseRouting in a classic pipeline.");
    }
}
