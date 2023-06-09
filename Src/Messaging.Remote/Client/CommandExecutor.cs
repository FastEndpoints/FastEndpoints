using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

internal interface ICommandExecutor { }

internal class BaseCommandExecutor<TCommand, TResult> where TCommand : class where TResult : class
{
    protected readonly CallInvoker _invoker;
    protected readonly Method<TCommand, TResult> _method;

    public BaseCommandExecutor(GrpcChannel channel, MethodType methodType)
    {
        _invoker = channel.CreateCallInvoker();
        _method = new Method<TCommand, TResult>(
            type: methodType,
            serviceName: typeof(TCommand).FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<TCommand>(),
            responseMarshaller: new MessagePackMarshaller<TResult>());
    }
}

internal interface IUnaryCommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    Task<TResult> Execute(ICommand<TResult> command, CallOptions opts);
}

internal sealed class UnaryCommandExecutor<TCommand, TResult> : BaseCommandExecutor<TCommand, TResult>, IUnaryCommandExecutor<TResult>
    where TCommand : class, ICommand<TResult>
    where TResult : class
{
    public UnaryCommandExecutor(GrpcChannel channel)
        : base(channel, MethodType.Unary) { }

    public Task<TResult> Execute(ICommand<TResult> cmd, CallOptions opts)
        => _invoker.AsyncUnaryCall(_method, null, opts, (TCommand)cmd).ResponseAsync;
}

internal interface IServerStreamCommandExecutor<TResult> : ICommandExecutor where TResult : class
{
    IAsyncStreamReader<TResult> Execute(IServerStreamCommand<TResult> command, CallOptions opts);
}

internal sealed class ServerStreamCommandExecutor<TCommand, TResult> : BaseCommandExecutor<TCommand, TResult>, IServerStreamCommandExecutor<TResult>
    where TCommand : class, IServerStreamCommand<TResult>
    where TResult : class
{
    public ServerStreamCommandExecutor(GrpcChannel channel)
        : base(channel, MethodType.ServerStreaming) { }

    public IAsyncStreamReader<TResult> Execute(IServerStreamCommand<TResult> cmd, CallOptions opts)
        => _invoker.AsyncServerStreamingCall(_method, null, opts, (TCommand)cmd).ResponseStream;
}