using System.Collections.Concurrent;

namespace TestCases.SkipHandlerIfResponseStarted;

public class Request
{
    public bool ShortCircuit { get; set; }
    public Guid CorrelationId { get; set; }
}

public class Response
{
    public string Source { get; set; }
    public bool HandlerRan { get; set; }
}

/// <summary>
/// opt-in short-circuit: handler is skipped when OnBeforeHandle sends a response.
/// </summary>
public class SkipEndpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("test-cases/skip-handler-if-response-started");
        AllowAnonymous();
        DontExecuteHandlerIfResponseStarted();
    }

    public override async Task OnBeforeHandleAsync(Request req, CancellationToken ct)
    {
        if (req.ShortCircuit)
            await Send.OkAsync(new() { Source = "before-handle", HandlerRan = false });
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
        => Send.OkAsync(
            new()
            {
                Source = "handler",
                HandlerRan = true
            });
}

/// <summary>
/// default behavior: handler still runs after OnBeforeHandle sends a response.
/// </summary>
public class ContinueEndpoint : Endpoint<Request, Response>
{
    internal static readonly ConcurrentDictionary<Guid, bool> HandlerExecutions = new();

    public override void Configure()
    {
        Post("test-cases/continue-handler-if-response-started");
        AllowAnonymous();
    }

    public override async Task OnBeforeHandleAsync(Request req, CancellationToken ct)
    {
        if (req.ShortCircuit)
            await Send.OkAsync(new() { Source = "before-handle", HandlerRan = false });
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        HandlerExecutions[req.CorrelationId] = true;

        return Task.CompletedTask;
    }
}