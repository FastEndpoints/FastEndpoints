using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

internal interface IUnaryCommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    Task<TResult> ExecuteUnary(ICommand<TResult> command, CallOptions opts);
}

internal sealed class UnaryCommandExecutor<TCommand, TResult> : BaseCommandExecutor<TCommand, TResult>, IUnaryCommandExecutor<TResult>
    where TCommand : class, ICommand<TResult>
    where TResult : class
{
    public UnaryCommandExecutor(GrpcChannel channel)
        : base(channel: channel,
               methodType: MethodType.Unary)
    { }

    public Task<TResult> ExecuteUnary(ICommand<TResult> cmd, CallOptions opts)
        => _invoker.AsyncUnaryCall(_method, null, opts, (TCommand)cmd).ResponseAsync;
}