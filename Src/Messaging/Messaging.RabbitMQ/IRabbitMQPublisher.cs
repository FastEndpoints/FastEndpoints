namespace FastEndpoints.Messaging.RabbitMQ;

/// <summary>
/// publishes FastEndpoints commands and events through RabbitMQ.
/// </summary>
public interface IRabbitMQPublisher
{
    /// <summary>
    /// sends a command to its registered command queue and waits for a publisher confirmation.
    /// </summary>
    Task SendAsync<TCommand>(TCommand command, CancellationToken ct = default) where TCommand : class, ICommand;

    /// <summary>
    /// publishes an event to its registered event exchange and waits for a publisher confirmation.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent eventModel, CancellationToken ct = default) where TEvent : class, IEvent;
}
