using Grpc.Core;

namespace FastEndpoints;

interface IVoidCommandExecutor : ICommandExecutor
{
    Task ExecuteVoid(ICommand command, CallOptions opts);
}

sealed class VoidCommandExecutor<TCommand>(ChannelBase channel, IRpcMarshallerFactory marshaller)
    : BaseCommandExecutor<TCommand, EmptyObject>(channel: channel, methodType: MethodType.Unary, marshaller: marshaller), IVoidCommandExecutor
    where TCommand : class, ICommand
{
    public Task ExecuteVoid(ICommand cmd, CallOptions opts)
        => Invoker.AsyncUnaryCall(Method, null, opts, (TCommand)cmd).ResponseAsync;
}