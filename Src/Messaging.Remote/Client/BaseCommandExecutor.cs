using Grpc.Core;

namespace FastEndpoints;

interface ICommandExecutor;

class BaseCommandExecutor<TCommand, TResult>(ChannelBase channel,
                                             MethodType methodType,
                                             string? endpointName = null)
    where TCommand : class
    where TResult : class
{
    protected readonly CallInvoker Invoker = channel.CreateCallInvoker();

    protected readonly Method<TCommand, TResult> Method = new(
        type: methodType,
        serviceName: endpointName ?? typeof(TCommand).FullName!,
        name: "",
        requestMarshaller: new MessagePackMarshaller<TCommand>(),
        responseMarshaller: new MessagePackMarshaller<TResult>());
}