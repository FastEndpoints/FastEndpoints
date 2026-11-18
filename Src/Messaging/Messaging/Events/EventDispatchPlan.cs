namespace FastEndpoints;

sealed class EventDispatchPlan
{
    readonly EventSubscription[] _subscriptions;
    readonly int[] _firstHandlerOfType;
    readonly IUntypedEventReceiver[] _receivers;

    internal IReadOnlyList<EventSubscription> Subscriptions => _subscriptions;

    // Empty unless test receivers are registered.
    internal bool HasReceivers => _receivers.Length != 0;

    internal EventDispatchPlan(IEnumerable<EventSubscription> subscriptions, IUntypedEventReceiver[] receivers)
    {
        _subscriptions = [.. subscriptions];
        _firstHandlerOfType = EventHandlerFilter<EventSubscription, SubscriptionInvocation>.CreateTypeIndex(_subscriptions);
        _receivers = receivers;
    }

    // The publishing bus has already recorded into its own receiver.
    internal void AddToReceivers(object model, object? alreadyRecorded)
    {
        foreach (var receiver in _receivers)
        {
            if (!ReferenceEquals(receiver, alreadyRecorded))
                receiver.AddEvent(model);
        }
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
        public static Type GetHandlerType(EventSubscription subscription)
            => subscription.HandlerType;

        public static Task Invoke(EventSubscription subscription, object model, CancellationToken ct)
            => subscription.Invoke(subscription.Handler, model, ct);
    }
}