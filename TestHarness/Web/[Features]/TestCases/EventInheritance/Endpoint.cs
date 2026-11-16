namespace TestCases.EventInheritance;

public class Request
{
    public Guid Id { get; set; }
}

public class Response
{
    public int BaseCalls { get; set; }
    public int DerivedCalls { get; set; }
    public int ReplacementCalls { get; set; }
}

public class BaseEvent : IEvent
{
    public Guid Id { get; init; }
    public Response Result { get; } = new();
}

public class DerivedEvent : BaseEvent;

public class DualHandler : IEventHandler<BaseEvent>, IEventHandler<DerivedEvent>
{
    Task IEventHandler<BaseEvent>.HandleAsync(BaseEvent e, CancellationToken ct)
    {
        e.Result.BaseCalls++;
        return Task.CompletedTask;
    }

    Task IEventHandler<DerivedEvent>.HandleAsync(DerivedEvent e, CancellationToken ct)
    {
        e.Result.DerivedCalls++;
        return Task.CompletedTask;
    }
}

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("test-cases/event-inheritance");
        AllowAnonymous();
        Description(b => b.ExcludeFromDescription());
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        BaseEvent model = new DerivedEvent { Id = req.Id };
        await PublishAsync(model, Mode.WaitForAll, ct);
        await Send.OkAsync(model.Result, ct);
    }
}
