using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace FastEndpoints;

public static class EventExtensions
{
    /// <summary>
    /// publish the event to all subscribers registered to handle this type of event.
    /// </summary>
    /// <typeparam name="TEvent">the type of the event model</typeparam>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    /// <param name="cancellation">an optional cancellation token</param>
    /// <returns>
    /// a Task that matches the wait mode specified.
    /// <see cref="Mode.WaitForNone" /> returns an already completed Task (fire and forget).
    /// <see cref="Mode.WaitForAny" /> returns a Task that will complete when any of the subscribers complete their work.
    /// <see cref="Mode.WaitForAll" /> return a Task that will complete only when all of the subscribers complete their work.
    /// </returns>
    public static Task PublishAsync<TEvent>(this TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default) where TEvent : IEvent
        => Cfg.ServiceResolver.Resolve<EventBus<TEvent>>().PublishAsync(eventModel, waitMode, cancellation);

    //key: tEvent
    //val: the PublishAsync compiled expression - Event<TEvent>.PublishAsync(...)
    static readonly ConcurrentDictionary<Type, Func<IEvent, Mode, CancellationToken, Task>> _publishFuncCache = new();

    static EventBus<T> CreateEventInstance<T>() where T : IEvent
        => Cfg.ServiceResolver.Resolve<EventBus<T>>();

    /// <summary>
    /// publish the event to all subscribers registered to handle this type of event.
    /// </summary>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    /// <param name="cancellation">an optional cancellation token</param>
    /// <returns>
    /// a Task that matches the wait mode specified.
    /// <see cref="Mode.WaitForNone" /> returns an already completed Task (fire and forget).
    /// <see cref="Mode.WaitForAny" /> returns a Task that will complete when any of the subscribers complete their work.
    /// <see cref="Mode.WaitForAll" /> return a Task that will complete only when all of the subscribers complete their work.
    /// </returns>
    public static Task PublishAsync(this IEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default)
    {
        var publishFunc = _publishFuncCache.GetOrAdd(eventModel.GetType(), PublishAsyncFuncFactory);

        return publishFunc(eventModel, waitMode, cancellation);

        static Func<IEvent, Mode, CancellationToken, Task> PublishAsyncFuncFactory(Type tEventModel)
        {
            var tBus = typeof(EventBus<>).MakeGenericType(tEventModel);
            var publishMethod = tBus.GetMethod(nameof(EventBus<IEvent>.PublishAsync))!;
            var eventParam = Expression.Parameter(typeof(IEvent), "eventModel");
            var waitModeParam = Expression.Parameter(typeof(Mode), "waitMode");
            var cancellationParam = Expression.Parameter(typeof(CancellationToken), "cancellation");
            var instanceParam = Expression.Variable(tBus, "instance");
            var eventModelCast = Expression.Convert(eventParam, tEventModel);
            var instanceInit = Expression.Assign(
                instanceParam,
                Expression.Call(typeof(EventExtensions), nameof(CreateEventInstance), [tEventModel]));
            var publishCall = Expression.Call(instanceParam, publishMethod, eventModelCast, waitModeParam, cancellationParam);
            var lambda = Expression.Lambda<Func<IEvent, Mode, CancellationToken, Task>>(
                Expression.Block(new[] { instanceParam }, instanceInit, publishCall),
                eventParam,
                waitModeParam,
                cancellationParam);

            return lambda.Compile();
        }
    }
}