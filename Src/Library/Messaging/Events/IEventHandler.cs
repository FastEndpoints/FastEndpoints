namespace FastEndpoints;

/// <summary>
/// marker interface for all event handlers
/// </summary>
public interface IEventHandler { }

/// <summary>
/// interface to be implemented by event handlers
/// </summary>
/// <typeparam name="TEvent">the type of the event model to be handled by this event handler</typeparam>
public interface IEventHandler<TEvent> : IEventHandler
{
    Task HandleAsync(TEvent eventModel, CancellationToken ct);
}