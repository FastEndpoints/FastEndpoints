using System.Runtime.ExceptionServices;
using System.Text.Json.Serialization;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> where TRequest : notnull
{
    static async ValueTask<TRequest> BindRequestAsync(EndpointDefinition def, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        var binder = (IRequestBinder<TRequest>)(def.EpRequestBinder ??= _tRequest.IsValueType
                                                                            ? new RequestBinder<TRequest>() // native aot cannot instantiate value type generic binders
                                                                            : ServiceResolver.Instance.Resolve<IRequestBinder<TRequest>>());

        if (def.MaxRequestSize > 0)
        {
            var feature = ctx.Features.Get<IHttpMaxRequestBodySizeFeature>();

            if (feature?.IsReadOnly is false)
                feature.MaxRequestBodySize = def.MaxRequestSize;
        }

        var binderCtx = new BinderContext(ctx, failures, def.SerializerContext, def.DontBindFormData, binder.RequiredProps);

        var req = await binder.BindAsync(binderCtx, ct);

        Cfg.BndOpts.Modifier?.Invoke(req, _tRequest, binderCtx, ct);

        return req;
    }

    static async Task<bool> FeatureFlagTriggered(IEnumerable<IFeatureFlag> flags, IEndpoint endpoint, CancellationToken ct)
    {
        foreach (var flag in flags)
        {
            if (await flag.IsEnabledAsync(endpoint))
                continue;

            if (!endpoint.HttpContext.ResponseStarted())
                await endpoint.HttpContext.Response.SendNotFoundAsync(ct);

            return true;
        }

        return false;
    }

    static async Task RunPreprocessors(List<IProcessor> preProcessors,
                                       TRequest? req,
                                       HttpContext ctx,
                                       List<ValidationFailure> validationFailures,
                                       CancellationToken ct)
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

    static async Task RunPostProcessors(List<IProcessor> postProcessors,
                                        TRequest? req,
                                        TResponse? resp,
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

    static Task<Void> AutoSendResponse(HttpContext ctx,
                                       TResponse responseDto,
                                       JsonSerializerContext? jsonSerializerContext,
                                       CancellationToken cancellation)
        => responseDto is null
               ? ctx.Response.SendNoContentAsync(cancellation)
               : ctx.Response.SendAsync(responseDto, ctx.Response.StatusCode, jsonSerializerContext, cancellation);

    internal static Task RunResponseInterceptor(IResponseInterceptor interceptor,
                                                object resp,
                                                int statusCode,
                                                HttpContext ctx,
                                                IReadOnlyCollection<ValidationFailure> validationFailures,
                                                CancellationToken cancellation)
        => interceptor.InterceptResponseAsync(resp, statusCode, ctx, validationFailures, cancellation);
}