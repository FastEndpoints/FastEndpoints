namespace NativeAotChecker.Endpoints.Events;

// covers handlers declared as records being picked up by the DiscoveredTypes generator
sealed class RecordEventHandlerRequest
{
    [QueryParam]
    public Guid Id { get; set; }
}

sealed class RecordEventHandlerEndpoint : Endpoint<RecordEventHandlerRequest, Guid>
{
    public override void Configure()
    {
        Post("record-event-handler");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RecordEventHandlerRequest req, CancellationToken ct)
    {
        var e = new RecordHandledEvent { Id = req.Id };
        await e.PublishAsync(Mode.WaitForAll, ct);
        await Send.OkAsync(e.Received, ct);
    }
}

sealed class RecordHandledEvent : IEvent
{
    public Guid Id { get; set; }
    public Guid Received { get; set; }
}

sealed record RecordHandledEventHandler : IEventHandler<RecordHandledEvent>
{
    public Task HandleAsync(RecordHandledEvent e, CancellationToken c)
    {
        e.Received = e.Id;

        return Task.CompletedTask;
    }
}
