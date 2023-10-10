using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

interface IEventPublisher : ICommandExecutor
{
    Task PublishEvent(IEvent evnt, CallOptions opts);
}

sealed class EventPublisher<TEvent> : BaseCommandExecutor<TEvent, EmptyObject>, IEventPublisher
    where TEvent : class, IEvent
{
    public EventPublisher(GrpcChannel channel)
        : base(
            channel: channel,
            methodType: MethodType.Unary,
            endpointName: typeof(TEvent).FullName + "/pub") { }

    public Task PublishEvent(IEvent evnt, CallOptions opts)
        => Invoker.AsyncUnaryCall(Method, null, opts, (TEvent)evnt).ResponseAsync;
}