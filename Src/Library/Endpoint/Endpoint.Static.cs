using System.Runtime.ExceptionServices;
using System.Text.Json.Serialization;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> where TRequest : notnull
{
    static async ValueTask<TRequest> BindRequestAsync(EndpointDefinition def,
                                                      HttpContext ctx,
                                                      List<ValidationFailure> failures,
                                                      CancellationToken ct)
    {
        var binder = (IRequestBinder<TRequest>)
            (def.RequestBinder ??= Conf.ServiceResolver.Resolve(typeof(IRequestBinder<TRequest>)));

        var binderCtx = new BinderContext(ctx, failures, def.SerializerContext, def.DontBindFormData);

        var req = await binder.BindAsync(
                      binderCtx,
                      ct);

        Conf.BndOpts.Modifier?.Invoke(req, _tRequest, binderCtx, ct);

        return req;
    }

    static async Task RunPreprocessors(List<object> preProcessors, TRequest req, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken ct)
    {
        if (preProcessors.Count == 0)
            return;

        var ppContext = new PreProcessorContext<TRequest>(req, ctx, validationFailures);

        foreach (var processor in preProcessors)
        {
            switch (processor)
            {
                case IGlobalPreProcessor gp:
                    await gp.PreProcessAsync(ppContext, ct);

                    break;
                case IPreProcessor<TRequest> pp:
                    await pp.PreProcessAsync(ppContext, ct);

                    break;
            }
        }
    }

    static async Task RunPostProcessors(List<object> postProcessors,
                                        TRequest req,
                                        TResponse resp,
                                        HttpContext ctx,
                                        ExceptionDispatchInfo? exceptionDispatchInfo,
                                        List<ValidationFailure> validationFailures,
                                        CancellationToken cancellation)
    {
        if (postProcessors.Count == 0)
            return;

        var ppContext = new PostProcessorContext<TRequest, TResponse>(req, resp, ctx, exceptionDispatchInfo, validationFailures);

        foreach (var processor in postProcessors)
        {
            switch (processor)
            {
                case IGlobalPostProcessor gp:
                    await gp.PostProcessAsync(ppContext, cancellation);

                    break;
                case IPostProcessor<TRequest, TResponse> pp:
                    await pp.PostProcessAsync(ppContext, cancellation);

                    break;
            }
        }
    }

    static Task RunResponseInterceptor(IResponseInterceptor interceptor,
                                       object resp,
                                       int statusCode,
                                       HttpContext ctx,
                                       List<ValidationFailure> validationFailures,
                                       CancellationToken cancellation)
        => interceptor.InterceptResponseAsync(resp, statusCode, ctx, validationFailures, cancellation);

    static Task AutoSendResponse(HttpContext ctx,
                                 TResponse responseDto,
                                 JsonSerializerContext? jsonSerializerContext,
                                 CancellationToken cancellation)
        => responseDto is null
               ? ctx.Response.SendNoContentAsync(cancellation)
               : ctx.Response.SendAsync(responseDto, ctx.Response.StatusCode, jsonSerializerContext, cancellation);

    static void AddProcessors(object[] processors, List<object> target)
    {
        for (var i = 0; i < processors.Length; i++)
        {
            var p = processors[i];

            if (!target.Contains(p, TypeEqualityComparer.Instance))
                target.Add(p);
        }
    }
}