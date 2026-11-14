using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// event notification bus which uses an in-process pub/sub messaging system
/// </summary>
/// <typeparam name="TEvent">the type of notification event dto</typeparam>
public sealed class EventBus<TEvent> : IExactEventBus where TEvent : notnull
{
    // Own the handler snapshot so cached type indexes cannot be invalidated by the caller.
    readonly IEventHandler<TEvent>[] _handlers;
    readonly int[] _firstHandlerOfType;
    readonly IEventReceiver<TEvent>? _testEventReceiver;
    readonly EventRegistry? _registry;
    EventSubscription[]? _subscriptions;

    // The singleton bus owns both views of its snapshot; exact-only use needs no untyped subscriptions.
    EventSubscription[] IExactEventBus.Subscriptions
        => LazyInitializer.EnsureInitialized(ref _subscriptions, () => [.. _handlers.Select(h => new EventSubscription(h.GetType(), h, InvokeExact))]);

    static Task InvokeExact(object handler, object eventModel, CancellationToken ct)
        => ((IEventHandler<TEvent>)handler).HandleAsync((TEvent)eventModel, ct);

    /// <summary>
    /// creates a provider-owned bus. the container uses this constructor to share exact subscriptions with inherited dispatch plans.
    /// </summary>
    /// <param name="provider">the owning service provider</param>
    /// <param name="eventHandlers">explicit handlers for this exact event contract</param>
    /// <param name="testEventReceiver">an optional test event receiver</param>
    public EventBus(IServiceProvider provider, IEnumerable<IEventHandler<TEvent>> eventHandlers, IEventReceiver<TEvent>? testEventReceiver = null)
    {
        _registry = provider.GetRequiredService<EventRegistry>();
        _handlers = [.. eventHandlers];
        if (_handlers.Length == 0)
            _handlers = _registry.ResolveDiscoveredHandlers<TEvent>();
        _firstHandlerOfType = EventHandlerFilter<IEventHandler<TEvent>, ExactInvocation>.CreateTypeIndex(_handlers);
        _testEventReceiver = testEventReceiver;
    }

    /// <summary>
    /// instantiates an event bus for the given event dto type.
    /// a nonempty supplied handler list is local and uses exact delivery, independent of host inheritance settings.
    /// a null or empty list falls back to discovery through the active host when available.
    /// </summary>
    /// <param name="eventHandlers">a collection of concrete event handler implementations that should receive notifications from this event bus</param>
    /// <param name="testEventReceiver">a test event receiver that can be used to assert receipt of events</param>
    public EventBus(IEnumerable<IEventHandler<TEvent>>? eventHandlers = null, IEventReceiver<TEvent>? testEventReceiver = null)
    {
        _handlers = eventHandlers?.ToArray() ?? [];

        if (_handlers.Length == 0 && !ServiceResolver.InstanceNotSet)
        {
            _registry = ServiceResolver.Instance.TryResolve<EventRegistry>();
            _handlers = _registry?.ResolveDiscoveredHandlers<TEvent>() ?? [];
        }

        _firstHandlerOfType = EventHandlerFilter<IEventHandler<TEvent>, ExactInvocation>.CreateTypeIndex(_handlers);
        _testEventReceiver = testEventReceiver;
    }

    /// <summary>
    /// publish the given model/dto to all the subscribers of the event notification
    /// </summary>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    /// <param name="cancellation">an optional cancellation token</param>
    /// <returns>
    /// a Task that matches the wait mode specified.
    /// <see cref="Mode.WaitForNone" /> returns an already completed Task (fire and forget).
    /// <see cref="Mode.WaitForAny" /> returns a Task that will complete when any of the subscribers complete their work.
    /// <see cref="Mode.WaitForAll" /> return a Task that will complete only when all the subscribers complete their work.
    /// </returns>
    public Task PublishAsync(TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default)
        => Execute(eventModel, waitMode, null, cancellation);

    /// <summary>
    /// publish the given model/dto to a subset of the subscribers of the event notification
    /// </summary>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="handlerFilter">
    /// a predicate for selecting which of the registered event handlers should be executed. if the predicate returns <c>false</c> for a particular event
    /// handler, that handler will not be executed during the invocation. evaluated once per implementation type per publish,
    /// with the result shared by all subscriptions on that implementation.
    /// </param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    /// <param name="cancellation">an optional cancellation token</param>
    /// <returns>
    /// a Task that matches the wait mode specified.
    /// <see cref="Mode.WaitForNone" /> returns an already completed Task (fire and forget).
    /// <see cref="Mode.WaitForAny" /> returns a Task that will complete when any of the subscribers complete their work.
    /// <see cref="Mode.WaitForAll" /> return a Task that will complete only when all the subscribers complete their work.
    /// </returns>
    public Task PublishFilteredAsync(TEvent eventModel, Func<Type, bool> handlerFilter, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default)
        => Execute(eventModel, waitMode, handlerFilter, cancellation);

    Task Execute(TEvent eventModel, Mode waitMode, Func<Type, bool>? handlerFilter, CancellationToken ct)
    {
        _testEventReceiver?.AddEvent(eventModel);

        if (_registry is not null && _registry.Options.Freeze())
            return _registry.GetDispatchPlan(eventModel.GetType()).Execute(eventModel, waitMode, handlerFilter, ct);

        var handlers = _handlers;
        if (handlerFilter is not null)
            handlers = EventHandlerFilter<IEventHandler<TEvent>, ExactInvocation>.Filter(_handlers, _firstHandlerOfType, handlerFilter);

        return EventExecution<IEventHandler<TEvent>, TEvent, ExactInvocation>.Execute(handlers, eventModel, waitMode, ct);
    }

    readonly struct ExactInvocation : IEventInvocation<IEventHandler<TEvent>, TEvent>, IEventHandlerType<IEventHandler<TEvent>>
    {
        public static Type GetHandlerType(IEventHandler<TEvent> handler)
            => handler.GetType();

        public static Task Invoke(IEventHandler<TEvent> handler, TEvent model, CancellationToken ct)
            => handler.HandleAsync(model, ct);
    }
}
