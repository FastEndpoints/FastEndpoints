using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

static class X402HttpContextExtensions
{
    extension(HttpContext ctx)
    {
        internal void SetX402RequestContext(X402RequestContext requestContext)
            => ctx.Items[X402CtxKey.RequestContext] = requestContext;

        internal X402RequestContext? GetX402RequestContext()
            => ctx.Items.TryGetValue(X402CtxKey.RequestContext, out var value) ? value as X402RequestContext : null;
    }
}

static class X402CtxKey
{
    internal const string RequestContext = "x402:0";
}