using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Pre and Post Processors in AOT mode
public sealed class ProcessorTestRequest
{
    public string Input { get; set; } = string.Empty;
}

public sealed class ProcessorTestResponse
{
    public string Input { get; set; } = string.Empty;
    public bool PreProcessorRan { get; set; }
    public bool PostProcessorRan { get; set; }
    public string ProcessedBy { get; set; } = string.Empty;
}

// Non-generic pre-processor
public sealed class AotPreProcessor : IPreProcessor<ProcessorTestRequest>
{
    public Task PreProcessAsync(IPreProcessorContext<ProcessorTestRequest> ctx, CancellationToken ct)
    {
        // Store flag in HttpContext.Items for post-processor to access
        ctx.HttpContext.Items["PreProcessorRan"] = true;
        ctx.HttpContext.Items["ProcessedBy"] = "AotPreProcessor";
        return Task.CompletedTask;
    }
}

// Non-generic post-processor  
public sealed class AotPostProcessor : IPostProcessor<ProcessorTestRequest, ProcessorTestResponse>
{
    // Static flag to verify post-processor ran (since response is already sent)
    public static bool DidRun { get; set; }
    public static string LastInput { get; set; } = string.Empty;

    public Task PostProcessAsync(IPostProcessorContext<ProcessorTestRequest, ProcessorTestResponse> ctx, CancellationToken ct)
    {
        DidRun = true;
        LastInput = ctx.Request?.Input ?? "null";
        return Task.CompletedTask;
    }
}

public sealed class ProcessorTestEndpoint : Endpoint<ProcessorTestRequest, ProcessorTestResponse>
{
    public override void Configure()
    {
        Post("processor-test");
        AllowAnonymous();
        PreProcessors(new AotPreProcessor());
        PostProcessors(new AotPostProcessor());
        SerializerContext<ProcessorTestSerCtx>();
    }

    public override async Task HandleAsync(ProcessorTestRequest req, CancellationToken ct)
    {
        var preProcessorRan = HttpContext.Items.TryGetValue("PreProcessorRan", out var ran) && (bool)ran!;
        var processedBy = HttpContext.Items.TryGetValue("ProcessedBy", out var by) ? by?.ToString() : "";

        await Send.OkAsync(new ProcessorTestResponse
        {
            Input = req.Input,
            PreProcessorRan = preProcessorRan,
            PostProcessorRan = false, // Will be set via verification endpoint
            ProcessedBy = processedBy ?? ""
        }, ct);
    }
}

// Verification endpoint to check if post-processor ran
public sealed class VerifyProcessorResponse
{
    public bool PostProcessorRan { get; set; }
    public string LastInput { get; set; } = string.Empty;
}

public sealed class VerifyProcessorEndpoint : EndpointWithoutRequest<VerifyProcessorResponse>
{
    public override void Configure()
    {
        Get("verify-processor");
        AllowAnonymous();
        SerializerContext<ProcessorTestSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = new VerifyProcessorResponse
        {
            PostProcessorRan = AotPostProcessor.DidRun,
            LastInput = AotPostProcessor.LastInput
        };
        
        // Reset for next test
        AotPostProcessor.DidRun = false;
        AotPostProcessor.LastInput = string.Empty;
        
        await Send.OkAsync(response, ct);
    }
}

[JsonSerializable(typeof(ProcessorTestRequest))]
[JsonSerializable(typeof(ProcessorTestResponse))]
[JsonSerializable(typeof(VerifyProcessorResponse))]
public partial class ProcessorTestSerCtx : JsonSerializerContext;
