using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;
using static FastEndpoints.Constants;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull
{
    private static async Task RunPostProcessors(object? postProcessors, TRequest req, TResponse resp, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (postProcessors is not null)
        {
            foreach (var pp in (IPostProcessor<TRequest, TResponse>[])postProcessors)
                await pp.PostProcessAsync(req, resp, ctx, validationFailures, cancellation);
        }
    }

    private static async Task RunPreprocessors(object? preProcessors, TRequest req, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (preProcessors is not null)
        {
            foreach (var p in (IPreProcessor<TRequest>[])preProcessors)
                await p.PreProcessAsync(req, ctx, validationFailures, cancellation);
        }
    }

    private static Task AutoSendResponse(HttpContext ctx, TResponse? responseDto, JsonSerializerContext? jsonSerializerContext, CancellationToken cancellation)
    {
        return responseDto is null
               ? ctx.Response.SendNoContentAsync(cancellation)
               : ctx.Response.SendAsync(responseDto, 200, jsonSerializerContext, cancellation);
    }

    private static readonly Action<RouteHandlerBuilder> ClearDefaultAcceptsProducesMetadata = b =>
    {
        b.Add(epBuilder =>
        {
            foreach (var m in epBuilder.Metadata.Where(o => o.GetType().Name is ProducesMetadata or AcceptsMetaData).ToArray())
                epBuilder.Metadata.Remove(m);
        });
    };
}