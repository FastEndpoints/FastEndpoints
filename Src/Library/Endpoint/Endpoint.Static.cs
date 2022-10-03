using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new()
{
    private static async Task RunPostProcessors(HashSet<object> postProcessors, TRequest req, TResponse resp, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        foreach (var p in postProcessors)
        {
            switch (p)
            {
                case IGlobalPostProcessor gp:
                    await gp.PostProcessAsync(req, resp, ctx, validationFailures, cancellation);
                    break;
                case IPostProcessor<TRequest, TResponse> pp:
                    await pp.PostProcessAsync(req, resp, ctx, validationFailures, cancellation);
                    break;
            }
        }
    }

    private static async Task RunPreprocessors(HashSet<object> preProcessors, TRequest req, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        foreach (var p in preProcessors)
        {
            switch (p)
            {
                case IGlobalPreProcessor gp:
                    await gp.PreProcessAsync(req, ctx, validationFailures, cancellation);
                    break;
                case IPreProcessor<TRequest> pr:
                    await pr.PreProcessAsync(req, ctx, validationFailures, cancellation);
                    break;
            }
        }
    }

    private static Task AutoSendResponse(HttpContext ctx, TResponse responseDto, JsonSerializerContext? jsonSerializerContext, CancellationToken cancellation)
    {
        return responseDto is null
               ? ctx.Response.SendNoContentAsync(cancellation)
               : ctx.Response.SendAsync(responseDto, 200, jsonSerializerContext, cancellation);
    }
}