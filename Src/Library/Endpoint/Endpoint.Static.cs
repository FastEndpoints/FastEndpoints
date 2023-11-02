using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Text.Json.Serialization;

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

    static async Task RunPostProcessors(List<object> postProcessors,
                                        TRequest req,
                                        TResponse resp,
                                        HttpContext ctx,
                                        ExceptionDispatchInfo? exceptionDispatchInfo,
                                        List<ValidationFailure> validationFailures,
                                        CancellationToken cancellation)
    {
        var context = new PostProcessorContext<TRequest, TResponse>
        {
            Request = req,
            Response = resp,
            HttpContext = ctx,
            ExceptionDispatchInfo = exceptionDispatchInfo,
            ValidationFailures = validationFailures
        };

        foreach (var processor in postProcessors)
        {
            switch (processor)
            {
                case IGlobalPostProcessor gp:
                    await gp.PostProcessAsync(context, cancellation);
                    break;
                case IPostProcessor<TRequest, TResponse> pp:
                    await pp.PostProcessAsync(context, cancellation);
                    break;
            }
        }
    }

    static async Task RunPreprocessors(List<object> preProcessors,
                                       TRequest req,
                                       HttpContext ctx,
                                       List<ValidationFailure> validationFailures,
                                       CancellationToken ct)
    {
        var context = new PreProcessorContext<TRequest>
        {
            Request = req,
            HttpContext = ctx,
            ValidationFailures = validationFailures
        };

        foreach (var processor in preProcessors)
        {
            switch (processor)
            {
                case IGlobalPreProcessor gp:
                    await gp.PreProcessAsync(context, ct);
                    break;
                case IPreProcessor<TRequest> pp:
                    await pp.PreProcessAsync(context, ct);
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