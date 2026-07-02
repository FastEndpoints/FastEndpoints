using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Net.Http.Headers;
using static FastEndpoints.Config;

namespace FastEndpoints;

internal sealed class FeRequestHandler : IResult
{
    internal static FeRequestHandler Instance { get; } = new();

    public Task ExecuteAsync(HttpContext ctx)
    {
        var endpoint = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!).Endpoint!;
        var epDef = endpoint.Metadata.GetMetadata<EndpointDefinition>()!;

        if (epDef.HitCounter is not null)
        {
            var hdrName = epDef.HitCounter.HeaderName ?? ThrOpts.HeaderName ?? "X-Forwarded-For";

            if (!ctx.Request.Headers.TryGetValue(hdrName, out var hdrVal))
            {
                hdrVal = ctx.Connection.RemoteIpAddress?.ToString();

                if (hdrVal.Count == 0)
                {
                    ctx.Response.StatusCode = 403;

                    return ctx.Response.WriteAsync("Forbidden by rate limiting middleware!", ctx.RequestAborted);
                }
            }

            if (epDef.HitCounter.LimitReached(hdrVal[0]!))
            {
                ctx.Response.StatusCode = 429;

                return ctx.Response.WriteAsync(ThrOpts.Message ?? "You are requesting this endpoint too frequently!", ctx.RequestAborted);
            }
        }

        if (!ctx.Request.Headers.ContainsKey(HeaderNames.ContentType))
        {
            // if all 3 conditions are true:
            //   1.) request doesn't contain any content-type headers
            //   2.) the matched endpoint declares accepts metadata
            //   3.) that metadata doesn't declare wildcard content-type support
            // then a 415 response needs to be sent to the client.
            // we don't need to check for mismatched content-types (between request and endpoint)
            // because routing middleware already takes care of that.
            //
            // read this from the matched endpoint's own metadata (rather than a value cached on the
            // shared EndpointDefinition) because a single EndpointDefinition can back multiple routes
            // that each declare different accepts requirements - e.g. one route's DTO properties are
            // all satisfied by that route's own {placeholders} while another route lacks some of them
            // and therefore still needs a JSON body.
            var acceptsMeta = endpoint.Metadata.GetMetadata<IAcceptsMetadata>();

            if (acceptsMeta is not null && !acceptsMeta.ContentTypes.Contains("*/*"))
            {
                ctx.Response.StatusCode = 415;

                return ctx.Response.StartAsync(ctx.RequestAborted);
            }
        }

        var epInstance = EndpointBootstrap.CreateEndpoint(ctx, epDef);

        // ReSharper disable SuspiciousTypeConversion.Global
        if (epDef.Disposable)
            ctx.Response.RegisterForDispose((IDisposable)epInstance);
        if (epDef.DisposableAsync)
            ctx.Response.RegisterForDisposeAsync((IAsyncDisposable)epInstance); // ReSharper restore SuspiciousTypeConversion.Global

        ResponseCacheExecutor.Execute(ctx, epDef.ResponseCacheSettings);

        return epInstance.ExecAsync(ctx.RequestAborted);
    }
}