using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: OnBeforeValidate / OnAfterValidate lifecycle hooks in AOT mode
public sealed class LifecycleHooksRequest
{
    public string Input { get; set; } = string.Empty;
    public string TransformedByBefore { get; set; } = string.Empty;
    public string TransformedByAfter { get; set; } = string.Empty;
}

public sealed class LifecycleHooksResponse
{
    public string Input { get; set; } = string.Empty;
    public string TransformedByBefore { get; set; } = string.Empty;
    public string TransformedByAfter { get; set; } = string.Empty;
    public bool BeforeValidateRan { get; set; }
    public bool AfterValidateRan { get; set; }
}

public sealed class OnBeforeAfterEndpoint : Endpoint<LifecycleHooksRequest, LifecycleHooksResponse>
{
    public override void Configure()
    {
        Post("lifecycle-hooks");
        AllowAnonymous();
        SerializerContext<LifecycleHooksSerCtx>();
    }

    public override void OnBeforeValidate(LifecycleHooksRequest req)
    {
        // Transform input in OnBeforeValidate
        req.TransformedByBefore = $"BEFORE:{req.Input}";
    }

    public override void OnAfterValidate(LifecycleHooksRequest req)
    {
        // Transform input in OnAfterValidate
        req.TransformedByAfter = $"AFTER:{req.Input}";
    }

    public override async Task HandleAsync(LifecycleHooksRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new LifecycleHooksResponse
        {
            Input = req.Input,
            TransformedByBefore = req.TransformedByBefore,
            TransformedByAfter = req.TransformedByAfter,
            BeforeValidateRan = req.TransformedByBefore.StartsWith("BEFORE:"),
            AfterValidateRan = req.TransformedByAfter.StartsWith("AFTER:")
        }, ct);
    }
}

[JsonSerializable(typeof(LifecycleHooksRequest))]
[JsonSerializable(typeof(LifecycleHooksResponse))]
public partial class LifecycleHooksSerCtx : JsonSerializerContext;
