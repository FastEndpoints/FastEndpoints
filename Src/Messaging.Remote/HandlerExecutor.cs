using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

internal sealed class HandlerExecutor<TCommand, THandler, TResult>
    where TCommand : class, ICommand<TResult>
    where THandler : ICommandHandler<TCommand, TResult>
    where TResult : class
{
    private readonly IServiceProvider _provider;

    public HandlerExecutor(IServiceProvider provider)
    {
        _provider = provider;
    }

    internal Task<TResult> Execute(TCommand cmd, ServerCallContext ctx)
    {
        var handler = _provider.GetRequiredService<THandler>();
        return handler.ExecuteAsync(cmd, ctx.CancellationToken);
    }
}
