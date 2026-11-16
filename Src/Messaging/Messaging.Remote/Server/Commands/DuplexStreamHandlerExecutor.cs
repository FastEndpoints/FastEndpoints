using Grpc.AspNetCore.Server.Model;
using Grpc.Core;

namespace FastEndpoints;

sealed class DuplexStreamHandlerExecutor<TCommand, THandler, TResult>
    : BaseHandlerExecutor<TCommand, THandler, TResult, DuplexStreamHandlerExecutor<TCommand, THandler, TResult>>
    where TCommand : class, IDuplexStreamCommand<TResult>
    where THandler : class, IDuplexStreamCommandHandler<TCommand, TResult>
    where TResult : class
{
    protected override MethodType MethodType()
        => Grpc.Core.MethodType.DuplexStreaming;

    protected override void AddMethodToCtx(ServiceMethodProviderContext<DuplexStreamHandlerExecutor<TCommand, THandler, TResult>> ctx,
                                           Method<TCommand, TResult> method,
                                           List<object> metadata)
        => ctx.AddDuplexStreamingMethod(method, metadata, ExecuteDuplexStream);

    async Task ExecuteDuplexStream(DuplexStreamHandlerExecutor<TCommand, THandler, TResult> _,
                                   IAsyncStreamReader<TCommand> requestStream,
                                   IServerStreamWriter<TResult> responseStream,
                                   ServerCallContext ctx)
    {
        using var cts = LinkWithAppStopping(ctx);
        var handler = CreateHandler(ctx);

        await foreach (var item in handler.ExecuteAsync(requestStream.ReadAllAsync(cts.Token), cts.Token))
            await responseStream.WriteAsync(item, cts.Token);
    }
}
