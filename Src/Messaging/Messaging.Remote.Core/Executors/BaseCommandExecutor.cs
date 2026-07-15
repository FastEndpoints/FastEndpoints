using Grpc.Core;

namespace FastEndpoints;

/// <summary>
/// marker interface for a command executor
/// </summary>
public interface ICommandExecutor;

class BaseCommandExecutor<TCommand, TResult>(ChannelBase channel, MethodType methodType, IRpcMarshallerFactory marshaller, string? endpointName = null)
    where TCommand : class
    where TResult : class
{
    protected readonly CallInvoker Invoker = channel.CreateCallInvoker();

    protected readonly Method<TCommand, TResult> Method = new(
        type: methodType,
        serviceName: endpointName ?? typeof(TCommand).FullName!,
        //event hubs supply their own explicit "sub"/"pub" endpoint name; only commands are bound under the wire format's method name
        name: endpointName is null ? marshaller.MethodName : "",
        requestMarshaller: marshaller.Create<TCommand>(),
        responseMarshaller: marshaller.Create<TResult>());
}