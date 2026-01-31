using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Generic Pre/Post Processors in AOT mode  
public sealed class GenericProcessorRequest
{
    public string Input { get; set; } = string.Empty;
    public int Value { get; set; }
}

public sealed class GenericProcessorResponse
{
    public string Input { get; set; } = string.Empty;
    public int Value { get; set; }
    public bool GenericPreProcessorRan { get; set; }
    public string PreProcessorRequestType { get; set; } = string.Empty;
}

// Generic pre-processor that works with any request type
public sealed class AotGenericPreProcessor<TReq> : IPreProcessor<TReq>
{
    public Task PreProcessAsync(IPreProcessorContext<TReq> ctx, CancellationToken ct)
    {
        ctx.HttpContext.Items["GenericPreProcessorRan"] = true;
        ctx.HttpContext.Items["PreProcessorRequestType"] = typeof(TReq).Name;
        return Task.CompletedTask;
    }
}

// Generic post-processor
public sealed class AotGenericPostProcessor<TReq, TRes> : IPostProcessor<TReq, TRes>
{
    public static bool DidRun { get; set; }
    public static string LastRequestType { get; set; } = string.Empty;
    public static string LastResponseType { get; set; } = string.Empty;

    public Task PostProcessAsync(IPostProcessorContext<TReq, TRes> ctx, CancellationToken ct)
    {
        DidRun = true;
        LastRequestType = typeof(TReq).Name;
        LastResponseType = typeof(TRes).Name;
        return Task.CompletedTask;
    }
}

public sealed class GenericProcessorEndpoint : Endpoint<GenericProcessorRequest, GenericProcessorResponse>
{
    public override void Configure()
    {
        Post("generic-processor");
        AllowAnonymous();
        PreProcessors(new AotGenericPreProcessor<GenericProcessorRequest>());
        PostProcessors(new AotGenericPostProcessor<GenericProcessorRequest, GenericProcessorResponse>());
        SerializerContext<GenericProcessorSerCtx>();
    }

    public override async Task HandleAsync(GenericProcessorRequest req, CancellationToken ct)
    {
        var genericPreProcessorRan = HttpContext.Items.TryGetValue("GenericPreProcessorRan", out var ran) && (bool)ran!;
        var requestType = HttpContext.Items.TryGetValue("PreProcessorRequestType", out var type) ? type?.ToString() : "";

        await Send.OkAsync(new GenericProcessorResponse
        {
            Input = req.Input,
            Value = req.Value,
            GenericPreProcessorRan = genericPreProcessorRan,
            PreProcessorRequestType = requestType ?? ""
        }, ct);
    }
}

// Verification endpoint
public sealed class VerifyGenericProcessorResponse
{
    public bool PostProcessorRan { get; set; }
    public string LastRequestType { get; set; } = string.Empty;
    public string LastResponseType { get; set; } = string.Empty;
}

public sealed class VerifyGenericProcessorEndpoint : EndpointWithoutRequest<VerifyGenericProcessorResponse>
{
    public override void Configure()
    {
        Get("verify-generic-processor");
        AllowAnonymous();
        SerializerContext<GenericProcessorSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var response = new VerifyGenericProcessorResponse
        {
            PostProcessorRan = AotGenericPostProcessor<GenericProcessorRequest, GenericProcessorResponse>.DidRun,
            LastRequestType = AotGenericPostProcessor<GenericProcessorRequest, GenericProcessorResponse>.LastRequestType,
            LastResponseType = AotGenericPostProcessor<GenericProcessorRequest, GenericProcessorResponse>.LastResponseType
        };

        // Reset for next test
        AotGenericPostProcessor<GenericProcessorRequest, GenericProcessorResponse>.DidRun = false;
        AotGenericPostProcessor<GenericProcessorRequest, GenericProcessorResponse>.LastRequestType = string.Empty;
        AotGenericPostProcessor<GenericProcessorRequest, GenericProcessorResponse>.LastResponseType = string.Empty;

        await Send.OkAsync(response, ct);
    }
}

[JsonSerializable(typeof(GenericProcessorRequest))]
[JsonSerializable(typeof(GenericProcessorResponse))]
[JsonSerializable(typeof(VerifyGenericProcessorResponse))]
public partial class GenericProcessorSerCtx : JsonSerializerContext;
