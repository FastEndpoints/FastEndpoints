namespace FastEndpoints;

sealed class EventDispatchPlan
{
    readonly EventSubscription[] _subscriptions;
    readonly int[] _firstHandlerOfType;

    internal IReadOnlyList<EventSubscription> Subscriptions => _subscriptions;

    internal EventDispatchPlan(IEnumerable<EventSubscription> subscriptions)
    {
        _subscriptions = subscriptions.ToArray();
        _firstHandlerOfType = EventHandlerFilter<EventSubscription, SubscriptionInvocation>.CreateTypeIndex(_subscriptions);
    }

    internal Task Execute(object model, Mode mode, Func<Type, bool>? filter, CancellationToken ct)
    {
        var subscriptions = filter is null
                                ? _subscriptions
                                : EventHandlerFilter<EventSubscription, SubscriptionInvocation>.Filter(_subscriptions, _firstHandlerOfType, filter);
        return EventExecution<EventSubscription, object, SubscriptionInvocation>.Execute(subscriptions, model, mode, ct);
    }

    readonly struct SubscriptionInvocation : IEventInvocation<EventSubscription, object>, IEventHandlerType<EventSubscription>
    {
        public static Type GetHandlerType(EventSubscription subscription) => subscription.HandlerType;

        public static Task Invoke(EventSubscription subscription, object model, CancellationToken ct)
            => subscription.Invoke(subscription.Handler, model, ct);
    }
}
