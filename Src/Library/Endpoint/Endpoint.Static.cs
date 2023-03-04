using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull
{
    private static async ValueTask<TRequest> BindRequestAsync(EndpointDefinition def,
                                                              HttpContext ctx,
                                                              List<ValidationFailure> failures,
                                                              CancellationToken ct)
    {
        var binder = (IRequestBinder<TRequest>)
            (def.RequestBinder ??= FastEndpoints.Config.ServiceResolver.Resolve(typeof(IRequestBinder<TRequest>)));

        var binderCtx = new BinderContext(ctx, failures, def.SerializerContext, def.DontBindFormData);

        var req = await binder.BindAsync(
            binderCtx,
            ct);

        FastEndpoints.Config.BndOpts.Modifier?.Invoke(req, tRequest, binderCtx, ct);

        return req;
    }

    private static async Task RunPostProcessors(List<object> postProcessors,
                                                TRequest req,
                                                TResponse resp,
                                                HttpContext ctx,
                                                List<ValidationFailure> validationFailures,
                                                CancellationToken cancellation)
    {
        for (var i = 0; i < postProcessors.Count; i++)
        {
            switch (postProcessors[i])
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

    private static async Task RunPreprocessors(List<object> preProcessors,
                                               TRequest req,
                                               HttpContext ctx,
                                               List<ValidationFailure> validationFailures,
                                               CancellationToken ct)
    {
        for (var i = 0; i < preProcessors.Count; i++)
        {
            switch (preProcessors[i])
            {
                case IGlobalPreProcessor gp:
                    await gp.PreProcessAsync(req, ctx, validationFailures, ct);
                    break;
                case IPreProcessor<TRequest> pr:
                    await pr.PreProcessAsync(req, ctx, validationFailures, ct);
                    break;
            }
        }
    }

    private static Task RunResponseInterceptor(IResponseInterceptor interceptor,
                                               object resp,
                                               int statusCode,
                                               HttpContext ctx,
                                               List<ValidationFailure> validationFailures,
                                               CancellationToken cancellation)
    {
        return interceptor.InterceptResponseAsync(resp, statusCode, ctx, validationFailures, cancellation);
    }

    private static Task AutoSendResponse(HttpContext ctx,
                                         TResponse responseDto,
                                         JsonSerializerContext? jsonSerializerContext,
                                         CancellationToken cancellation)
    {
        return responseDto is null
            ? ctx.Response.SendNoContentAsync(cancellation)
            : ctx.Response.SendAsync(responseDto, ctx.Response.StatusCode, jsonSerializerContext, cancellation);
    }

    private static void AddProcessors(object[] preProcessors, List<object> list)
    {
        for (var i = 0; i < preProcessors.Length; i++)
        {
            var p = preProcessors[i];

            if (!list.Contains(p, TypeEqualityComparer.Instance))
                list.Add(p);
        }
    }
}