using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

internal interface ICommandExecutor { }

internal interface ICommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    Task<TResult> Execute(ICommand<TResult> command, CancellationToken ct);
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
            requestMarshaller: new MsgPackMarshaller<TCommand>(),
            responseMarshaller: new MsgPackMarshaller<TResult>());
    }

    public Task<TResult> Execute(ICommand<TResult> cmd, CancellationToken ct)
    {
        var call = _invoker.AsyncUnaryCall(_method, null, new CallOptions(cancellationToken: ct), (TCommand)cmd);
        return call.ResponseAsync;
    }
}