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
    /// <summary>
    /// the handler logic for the event handler
    /// </summary>
    /// <param name="eventModel">the input event model</param>
    /// <param name="ct">optional cancellation token</param>
    Task HandleAsync(TEvent eventModel, CancellationToken ct = default);
}