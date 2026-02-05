namespace NativeAotChecker.Endpoints.Events;

sealed class EventPublishRequest
{
    [QueryParam]
    public Guid Id { get; set; }
}

sealed class EventPublishEndpoint : Endpoint<EventPublishRequest, Guid>
{
    public override void Configure()
    {
        Post("event-publish");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EventPublishRequest req, CancellationToken ct)
    {
        IEvent e = new EventHasBeenPublished(req.Id);
        await e.PublishAsync(Mode.WaitForAll, ct);
        await Send.OkAsync(((EventHasBeenPublished)e).Received, ct);
    }
}

sealed class EventHasBeenPublished(Guid Id) : IEvent
{
    public Guid Id { get; set; } = Id;
    public Guid Received { get; set; }
}

sealed class EventHasBeenPublishedHandler : IEventHandler<EventHasBeenPublished>
{
    public Task HandleAsync(EventHasBeenPublished e, CancellationToken c)
    {
        e.Received = e.Id;

        return Task.CompletedTask;
    }
}