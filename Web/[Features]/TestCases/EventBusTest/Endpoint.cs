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
        var evnt = new TestEvent { Id = 100 };
        await evnt.PublishAsync();
        await SendAsync(evnt.Id);
    }
}

sealed class TestEvent : IEvent
{
    public int Id { get; set; }
}

sealed class TestEventHandler : IEventHandler<TestEvent>
{
    public Task HandleAsync(TestEvent e, CancellationToken c)
    {
        e.Id = 200;
        return Task.CompletedTask;
    }
}