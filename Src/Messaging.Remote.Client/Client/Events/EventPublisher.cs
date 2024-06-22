using Grpc.Core;

namespace FastEndpoints;

interface IEventPublisher : ICommandExecutor
{
    Task PublishEvent(IEvent evnt, CallOptions opts);
}

sealed class EventPublisher<TEvent>(ChannelBase channel)
    : BaseCommandExecutor<TEvent, EmptyObject>(channel: channel, methodType: MethodType.Unary, endpointName: typeof(TEvent).FullName + "/pub"),
      IEventPublisher
    where TEvent : class, IEvent
{
    public Task PublishEvent(IEvent evnt, CallOptions opts)
        => Invoker.AsyncUnaryCall(Method, null, opts, (TEvent)evnt).ResponseAsync;
}