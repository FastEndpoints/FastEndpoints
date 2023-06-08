using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

internal interface ICommandExecutor { }

internal interface ICommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    Task<TResult> Execute(ICommand<TResult> command, CallOptions opts);
}

internal sealed class CommandExecutor<TCommand, TResult> : ICommandExecutor<TResult>
    where TCommand : class, ICommand<TResult>
    where TResult : class
{
    private readonly Method<TCommand, TResult> _method;
    private readonly CallInvoker _invoker;

    public CommandExecutor(GrpcChannel channel)
    {
        _invoker = channel.CreateCallInvoker();
        _method = new Method<TCommand, TResult>(
            type: MethodType.Unary,
            serviceName: typeof(TCommand).FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<TCommand>(),
            responseMarshaller: new MessagePackMarshaller<TResult>());
    }

    public Task<TResult> Execute(ICommand<TResult> cmd, CallOptions opts)
    {
        var call = _invoker.AsyncUnaryCall(_method, null, opts, (TCommand)cmd);
        return call.ResponseAsync;
    }
}