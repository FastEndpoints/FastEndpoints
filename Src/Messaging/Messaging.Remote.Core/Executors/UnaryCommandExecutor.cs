using Grpc.Core;

namespace FastEndpoints;

interface IUnaryCommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    Task<TResult> ExecuteUnary(ICommand<TResult> command, CallOptions opts);
}

sealed class UnaryCommandExecutor<TCommand, TResult>(ChannelBase channel)
    : BaseCommandExecutor<TCommand, TResult>(channel: channel, methodType: MethodType.Unary),
      IUnaryCommandExecutor<TResult>
    where TCommand : class, ICommand<TResult>
    where TResult : class
{
    public Task<TResult> ExecuteUnary(ICommand<TResult> cmd, CallOptions opts)
        => Invoker.AsyncUnaryCall(Method, null, opts, (TCommand)cmd).ResponseAsync;
}