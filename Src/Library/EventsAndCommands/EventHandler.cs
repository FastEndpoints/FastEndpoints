using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// inherit this base class to handle events published by the notification system
/// <para>WARNING: event handlers are singletons. DO NOT maintain state in them. Use the <c>Resolve*()</c> methods to obtain dependencies.</para>
/// </summary>
/// <typeparam name="TEvent">the type of the event to handle</typeparam>
public abstract class FastEventHandler<TEvent> : FastBaseHandler, IEventHandler<TEvent>, IServiceResolver where TEvent : notnull
{
    /// <summary>
    /// this method will be called when an event of the specified type is published.
    /// </summary>
    /// <param name="eventModel">the event model/dto received</param>
    /// <param name="ct">an optional cancellation token</param>
    public abstract Task HandleAsync(TEvent eventModel, CancellationToken ct);
}