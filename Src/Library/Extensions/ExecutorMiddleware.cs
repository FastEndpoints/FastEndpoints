using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
//using System.Collections.Concurrent;

namespace FastEndpoints;

internal class ExecutorMiddleware
{
    private const string authInvoked = "__AuthorizationMiddlewareWithEndpointInvoked";
    private const string corsInvoked = "__CorsMiddlewareWithEndpointInvoked";
    private readonly RequestDelegate _next;

    public ExecutorMiddleware(RequestDelegate next)
        => _next = next ?? throw new ArgumentNullException(nameof(next));

    public Task Invoke(HttpContext ctx)
    {
        var endpoint = ((IEndpointFeature)ctx.Features[Types.IEndpointFeature]!)?.Endpoint;

        if (endpoint is null) return _next(ctx);

        var epMetaData = endpoint.Metadata.GetMetadata<EndpointMetadata>();

        if (epMetaData is not null)
        {
            if (endpoint.Metadata.GetMetadata<IAuthorizeData>() != null && !ctx.Items.ContainsKey(authInvoked))
                ThrowAuthMiddlewareMissing(endpoint.DisplayName!);

            if (endpoint.Metadata.GetMetadata<ICorsMetadata>() != null && !ctx.Items.ContainsKey(corsInvoked))
                ThrowCORSMiddlewareMissing(endpoint.DisplayName!);

            var epInstance = (BaseEndpoint)epMetaData.InstanceCreator();

            ResolveServices(epInstance, ctx.RequestServices, epMetaData.ServiceBoundEpProps);

            ResponseCacheExecutor.Execute(ctx, endpoint.Metadata.GetMetadata<ResponseCacheAttribute>());

            return epInstance.ExecAsync(
                ctx,
                epMetaData.Validator,
                epMetaData.EndpointSettings.PreProcessors,
                epMetaData.EndpointSettings.PostProcessors,
                ctx.RequestAborted); //terminate middleware here. we're done executing
        }

        return _next(ctx); //this is not a fastendpoint, let next middleware handle it
    }

    private static void ResolveServices(object epInstance, IServiceProvider services, ServiceBoundEpProp[]? props)
    {
        if (props is null) return;

        for (int i = 0; i < props.Length; i++)
        {
            var p = props[i];
            p.PropSetter(epInstance, services.GetRequiredService(p.PropType));
        }
    }

    private static void ThrowAuthMiddlewareMissing(string epName)
    {
        throw new InvalidOperationException($"Endpoint {epName} contains authorization metadata, " +
            "but a middleware was not found that supports authorization." +
            Environment.NewLine +
            "Configure your application startup by adding app.UseAuthorization() in the application startup code. If there are calls to app.UseRouting() and app.UseFastEndpoints() the call to app.UseAuthorization() must go between them.");
    }

    private static void ThrowCORSMiddlewareMissing(string epName)
    {
        throw new InvalidOperationException($"Endpoint {epName} contains CORS metadata, " +
            "but a middleware was not found that supports CORS." +
            Environment.NewLine +
            "Configure your application startup by adding app.UseCors() in the application startup code. If there are calls to app.UseRouting() and app.UseFastEndpoints(), the call to app.UseCors() must go between them.");
    }
}

//internal class HitCounter
//{
//    private readonly ConcurrentDictionary<string, Counter> dic;

//    public HitCounter()
//    {

//    }

//    internal class Counter
//    {
//        private int _count;
//        private readonly DateTimeOffset _start;
//        private readonly int _limit;
//        private readonly TimeSpan _timeSpan;

//        public Counter(int limit, TimeSpan timeSpan)
//        {
//            _limit = limit;
//            _timeSpan = timeSpan;
//        }

//        internal void Increase() => Interlocked.Increment(ref _count);
//    }
//}