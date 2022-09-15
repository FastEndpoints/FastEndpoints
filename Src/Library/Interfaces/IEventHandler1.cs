namespace FastEndpoints;

internal interface IEventHandler<TEvent> : IEventHandler
{
    Task HandleAsync(TEvent eventModel, CancellationToken ct);
}
