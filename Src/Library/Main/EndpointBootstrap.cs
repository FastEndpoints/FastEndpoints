using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal static class EndpointBootstrap
{
    internal static BaseEndpoint CreateEndpoint(HttpContext ctx, EndpointDefinition epDef)
    {
        var epInstance = ctx.RequestServices.GetRequiredService<IEndpointFactory>().Create(epDef, ctx.RequestServices);
        epInstance.Definition = epDef;
        epInstance.HttpContext = ctx;
        ctx.Items[CtxKey.ValidationFailures] = epInstance.ValidationFailures;

        if (epDef.ToHeaderProps.Length > 0)
            ctx.Items[CtxKey.ToHeaderProps] = epDef.ToHeaderProps;

        return epInstance;
    }
}