using System.Collections.Concurrent;

namespace FastEndpoints;

/// <summary>
/// base class for the event bus
/// </summary>
public abstract class EventBase
{
    //key: TEvent 
    //val: unique list of event handler types (subscribers)
    internal static readonly ConcurrentDictionary<Type, HashSet<Type>> HandlerDict = new();
}

/// <summary>
/// event notification bus which uses an in-process pub/sub messaging system
/// </summary>
/// <typeparam name="TEvent">the type of notification event dto</typeparam>
public sealed class EventBus<TEvent> : EventBase where TEvent : notnull
{
    readonly IEventHandler<TEvent>[] _handlers = [];
    readonly IEventReceiver<TEvent>? _testEventReceiver;

    /// <summary>
    /// instantiates an event bus for the given event dto type.
    /// </summary>
    /// <param name="eventHandlers">a collection of concrete event handler implementations that should receive notifications from this event bus</param>
    /// <param name="testEventReceiver">a test event receiver that can be used to assert receipt of events</param>
    public EventBus(IEnumerable<IEventHandler<TEvent>>? eventHandlers = null, IEventReceiver<TEvent>? testEventReceiver = null)
    {
        if (eventHandlers is not null)
        {
            var hndlrArray = eventHandlers as IEventHandler<TEvent>[] ?? eventHandlers.ToArray();

            if (hndlrArray.Length > 0)
                _handlers = hndlrArray;
        }

        if (_handlers.Length == 0 && HandlerDict.TryGetValue(typeof(TEvent), out var hndlrs) && hndlrs.Count > 0)
            _handlers = hndlrs.Select(ServiceResolver.Instance.CreateSingleton).Cast<IEventHandler<TEvent>>().ToArray(); //ToArray() is essential here!!!

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
        => Execute(_handlers, eventModel, waitMode, null, _testEventReceiver, cancellation);

    /// <summary>
    /// publish the given model/dto to a subset of the subscribers of the event notification
    /// </summary>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="handlerFilter">
    /// a predicate for selecting which of the registered event handlers should be executed. if the predicate returns <c>false</c> for a particular event
    /// handler, that handler will not be executed during the invocation.
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
        => Execute(_handlers, eventModel, waitMode, handlerFilter, _testEventReceiver, cancellation);

    static Task Execute(IEventHandler<TEvent>[] handlers,
                        TEvent eventModel,
                        Mode waitMode,
                        Func<Type, bool>? handlerFilter,
                        IEventReceiver<TEvent>? testEventReceiver,
                        CancellationToken ct)
    {
        testEventReceiver?.AddEvent(eventModel);

        if (handlerFilter is not null)
            handlers = Filter(handlers, handlerFilter);

        if (handlers.Length == 0)
            return Task.CompletedTask;

        //note: the lambdas needed by the offloading modes live in their own methods on purpose. inlining them here makes roslyn
        //      hoist the display class to the top of this method, which would allocate it on every publish, including the
        //      zero allocation single handler fast path below. see .okf/gotchas.md
        switch (waitMode)
        {
            case Mode.WaitForNone:
                Offload(handlers, eventModel, ct);

                return Task.CompletedTask;

            case Mode.WaitForAny:
                return Task.WhenAny(OffloadAll(handlers, eventModel, ct));

            case Mode.WaitForAll:
            {
                if (handlers.Length == 1)
                    return handlers[0].HandleAsync(eventModel, ct);

                var tasks = new Task[handlers.Length];

                for (var i = 0; i < handlers.Length; i++)
                    tasks[i] = handlers[i].HandleAsync(eventModel, ct);

                return Task.WhenAll(tasks);
            }

            default:
                return Task.CompletedTask;
        }
    }

    static IEventHandler<TEvent>[] Filter(IEventHandler<TEvent>[] handlers, Func<Type, bool> handlerFilter)
        => handlers.Where(h => handlerFilter(h.GetType())).ToArray();

    //fire and forget. the handlers are offloaded so that a slow or synchronously throwing handler can neither block nor throw at the publisher.
    static void Offload(IEventHandler<TEvent>[] handlers, TEvent eventModel, CancellationToken ct)
    {
        foreach (var h in handlers)
            _ = Task.Run(() => h.HandleAsync(eventModel, ct), ct);
    }

    static Task[] OffloadAll(IEventHandler<TEvent>[] handlers, TEvent eventModel, CancellationToken ct)
    {
        var tasks = new Task[handlers.Length];

        for (var i = 0; i < handlers.Length; i++)
        {
            var h = handlers[i];
            tasks[i] = Task.Run(() => h.HandleAsync(eventModel, ct), ct);
        }

        return tasks;
    }
}