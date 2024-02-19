namespace TestCases.EventBusTest;

sealed class Endpoint : EndpointWithoutRequest<int>
{
    public override void Configure()
    {
        Get("/test-cases/event-bus-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var evnt = new TestEventBus { Id = 100 };
        await evnt.PublishAsync();
        await SendAsync(evnt.Id);
    }
}

sealed class TestEventBus : IEvent
{
    public int Id { get; set; }
}

sealed class TestEventBusHandler : IEventHandler<TestEventBus>
{
    public Task HandleAsync(TestEventBus e, CancellationToken c)
    {
        e.Id = 200;
        return Task.CompletedTask;
    }
}