using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

interface IVoidCommandExecutor : ICommandExecutor
{
    Task ExecuteVoid(ICommand command, CallOptions opts);
}

sealed class VoidCommandExecutor<TCommand> : BaseCommandExecutor<TCommand, EmptyObject>, IVoidCommandExecutor
    where TCommand : class, ICommand
{
    public VoidCommandExecutor(GrpcChannel channel)
        : base(
            channel: channel,
            methodType: MethodType.Unary) { }

    public Task ExecuteVoid(ICommand cmd, CallOptions opts)
        => Invoker.AsyncUnaryCall(Method, null, opts, (TCommand)cmd).ResponseAsync;
}