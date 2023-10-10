using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

interface ICommandExecutor { }

class BaseCommandExecutor<TCommand, TResult> where TCommand : class where TResult : class
{
    protected readonly CallInvoker Invoker;
    protected readonly Method<TCommand, TResult> Method;

    public BaseCommandExecutor(GrpcChannel channel, MethodType methodType, string? endpointName = null)
    {
        Invoker = channel.CreateCallInvoker();
        Method = new(
            type: methodType,
            serviceName: endpointName ?? typeof(TCommand).FullName!,
            name: "",
            requestMarshaller: new MessagePackMarshaller<TCommand>(),
            responseMarshaller: new MessagePackMarshaller<TResult>());
    }
}