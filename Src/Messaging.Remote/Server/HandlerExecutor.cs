using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal abstract class HandlerExecutorBase
{
    internal static IServiceProvider ServiceProvider { get; set; } = default!;
}

internal sealed class HandlerExecutor<TCommand, THandler, TResult> : HandlerExecutorBase
    where TCommand : class, ICommand<TResult>
    where THandler : ICommandHandler<TCommand, TResult>
    where TResult : class
{
    internal static Task<TResult> Execute(TCommand cmd, ServerCallContext ctx)
    {
        var handler = ServiceProvider.GetRequiredService<THandler>();
        return handler.ExecuteAsync(cmd, ctx.CancellationToken);
    }
}
