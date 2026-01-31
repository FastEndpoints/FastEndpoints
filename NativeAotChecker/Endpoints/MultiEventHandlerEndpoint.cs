using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Multiple event handlers for the same event in AOT mode
public sealed class MultiHandlerEvent : IEvent
{
    public Guid Id { get; set; }
    public List<string> HandlersExecuted { get; set; } = [];
}

public sealed class MultiHandlerEventHandler1 : IEventHandler<MultiHandlerEvent>
{
    public Task HandleAsync(MultiHandlerEvent e, CancellationToken ct)
    {
        e.HandlersExecuted.Add("Handler1");
        return Task.CompletedTask;
    }
}

public sealed class MultiHandlerEventHandler2 : IEventHandler<MultiHandlerEvent>
{
    public Task HandleAsync(MultiHandlerEvent e, CancellationToken ct)
    {
        e.HandlersExecuted.Add("Handler2");
        return Task.CompletedTask;
    }
}

public sealed class MultiHandlerEventHandler3 : IEventHandler<MultiHandlerEvent>
{
    public Task HandleAsync(MultiHandlerEvent e, CancellationToken ct)
    {
        e.HandlersExecuted.Add("Handler3");
        return Task.CompletedTask;
    }
}

public sealed class MultiEventHandlerRequest
{
    [QueryParam]
    public Guid Id { get; set; }
}

public sealed class MultiEventHandlerResponse
{
    public Guid Id { get; set; }
    public int HandlerCount { get; set; }
    public List<string> HandlersExecuted { get; set; } = [];
}

public sealed class MultiEventHandlerEndpoint : Endpoint<MultiEventHandlerRequest, MultiEventHandlerResponse>
{
    public override void Configure()
    {
        Post("multi-event-handler");
        AllowAnonymous();
        SerializerContext<MultiEventHandlerSerCtx>();
    }

    public override async Task HandleAsync(MultiEventHandlerRequest req, CancellationToken ct)
    {
        var e = new MultiHandlerEvent { Id = req.Id };
        await e.PublishAsync(Mode.WaitForAll, ct);

        await Send.OkAsync(new MultiEventHandlerResponse
        {
            Id = e.Id,
            HandlerCount = e.HandlersExecuted.Count,
            HandlersExecuted = e.HandlersExecuted
        }, ct);
    }
}

[JsonSerializable(typeof(MultiEventHandlerRequest))]
[JsonSerializable(typeof(MultiEventHandlerResponse))]
public partial class MultiEventHandlerSerCtx : JsonSerializerContext;
