using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal static class EndpointBootstrap
{
    internal static BaseEndpoint CreateEndpoint(HttpContext ctx, EndpointDefinition epDef)
    {
        var epInstance = ctx.RequestServices.GetRequiredService<IEndpointFactory>().Create(epDef, ctx);
        epInstance.Definition = epDef;
        epInstance.HttpContext = ctx;
        ctx.Items[CtxKey.ValidationFailures] = epInstance.ValidationFailures;

        var toHeaderProps = epDef.ToHeaderProps;

        if (toHeaderProps.Length > 0)
            ctx.Items[CtxKey.ToHeaderProps] = toHeaderProps;

        return epInstance;
    }
}
