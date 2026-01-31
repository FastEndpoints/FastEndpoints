using FastEndpoints;
using System.Diagnostics;

namespace NativeAotChecker.Endpoints;

// State bag for sharing between processors
public class RequestStateBag
{
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    
    public bool IsValidated { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;
    public Dictionary<string, object> Metadata { get; } = new();
}

// Pre-processor with state
public class ValidationPreProcessor : PreProcessor<ProcessorStateRequest, RequestStateBag>
{
    public override Task PreProcessAsync(IPreProcessorContext<ProcessorStateRequest> ctx, RequestStateBag state, CancellationToken ct)
    {
        // Validate and set state
        state.IsValidated = !string.IsNullOrEmpty(ctx.Request.Name);
        state.ValidationMessage = state.IsValidated ? "Valid" : "Invalid - Name is required";
        state.Metadata["ValidatedBy"] = nameof(ValidationPreProcessor);
        state.Metadata["ValidatedAt"] = DateTime.UtcNow;
        
        return Task.CompletedTask;
    }
}

// Post-processor with state
public class TimingPostProcessor : PostProcessor<ProcessorStateRequest, RequestStateBag, ProcessorStateResponse>
{
    public override Task PostProcessAsync(
        IPostProcessorContext<ProcessorStateRequest, ProcessorStateResponse> ctx, 
        RequestStateBag state, 
        CancellationToken ct)
    {
        // Access state set by pre-processor
        ctx.HttpContext.Response.Headers["X-Processing-Time"] = $"{state.ElapsedMilliseconds}ms";
        ctx.HttpContext.Response.Headers["X-Validated"] = state.IsValidated.ToString();
        
        return Task.CompletedTask;
    }
}

// Request/Response
public class ProcessorStateRequest
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class ProcessorStateResponse
{
    public bool IsValidated { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
    public long ProcessingTimeMs { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool ProcessorStateWorked { get; set; }
}

/// <summary>
/// Tests processor state sharing in AOT mode.
/// AOT ISSUE: ProcessorState&lt;TState&gt; uses generic lookup at runtime.
/// State bag instantiation may use Activator.CreateInstance.
/// Abstract PreProcessor/PostProcessor classes use generic dispatch.
/// </summary>
public class ProcessorStateEndpoint : Endpoint<ProcessorStateRequest, ProcessorStateResponse>
{
    public override void Configure()
    {
        Post("processor-state-test");
        AllowAnonymous();
        PreProcessor<ValidationPreProcessor>();
        PostProcessor<TimingPostProcessor>();
    }

    public override async Task HandleAsync(ProcessorStateRequest req, CancellationToken ct)
    {
        var state = ProcessorState<RequestStateBag>();
        
        await Send.OkAsync(new ProcessorStateResponse
        {
            IsValidated = state.IsValidated,
            ValidationMessage = state.ValidationMessage,
            ProcessingTimeMs = state.ElapsedMilliseconds,
            Metadata = state.Metadata,
            ProcessorStateWorked = state.IsValidated
        });
    }
}
