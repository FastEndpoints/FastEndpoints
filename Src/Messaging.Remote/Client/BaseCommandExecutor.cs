using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

internal interface ICommandExecutor { }

internal class BaseCommandExecutor<TCommand, TResult> where TCommand : class where TResult : class
{
    protected readonly CallInvoker _invoker;
    protected readonly Method<TCommand, TResult> _method;

    public BaseCommandExecutor(GrpcChannel channel, MethodType methodType, string? serviceName = null)
    {
        _invoker = channel.CreateCallInvoker();
        _method = new Method<TCommand, TResult>(
            type: methodType,
            serviceName: serviceName ?? typeof(TCommand).FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<TCommand>(),
            responseMarshaller: new MessagePackMarshaller<TResult>());
    }
}