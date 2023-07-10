using Grpc.Core;
using Grpc.Net.Client;

namespace FastEndpoints;

internal interface IEventPublisher : ICommandExecutor
{
    Task PublishEvent(IEvent evnt, CallOptions opts);
}

internal sealed class EventPublisher<TEvent> : BaseCommandExecutor<TEvent, EmptyObject>, IEventPublisher
    where TEvent : class, IEvent
{
    public EventPublisher(GrpcChannel channel)
        : base(channel, MethodType.Unary, typeof(TEvent).FullName + "/pub") { }

    public Task PublishEvent(IEvent evnt, CallOptions opts)
        => _invoker.AsyncUnaryCall(_method, null, opts, (TEvent)evnt).ResponseAsync;
}