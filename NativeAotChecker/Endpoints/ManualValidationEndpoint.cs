using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Manual validation with AddError in AOT mode
public sealed class ManualValidationRequest
{
    public string Username { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool ShouldFail { get; set; }
}

public sealed class ManualValidationResponse
{
    public string Message { get; set; } = string.Empty;
    public bool ValidationPassed { get; set; }
}

public sealed class ManualValidationEndpoint : Endpoint<ManualValidationRequest, ManualValidationResponse>
{
    public override void Configure()
    {
        Post("manual-validation");
        AllowAnonymous();
        SerializerContext<ManualValidationSerCtx>();
    }

    public override async Task HandleAsync(ManualValidationRequest req, CancellationToken ct)
    {
        // Manual validation using AddError
        if (string.IsNullOrWhiteSpace(req.Username))
        {
            AddError(r => r.Username, "Username is required");
        }

        if (req.Age < 0 || req.Age > 150)
        {
            AddError(r => r.Age, "Age must be between 0 and 150");
        }

        if (req.ShouldFail)
        {
            AddError("Intentional failure triggered");
        }

        // Check if we have validation errors and send appropriate response
        ThrowIfAnyErrors();

        await Send.OkAsync(new ManualValidationResponse
        {
            Message = $"Hello {req.Username}, you are {req.Age} years old",
            ValidationPassed = true
        }, ct);
    }
}

// Test: ValidationFailed method override
public sealed class CustomValidationFailureEndpoint : Endpoint<ManualValidationRequest, ManualValidationResponse>
{
    public override void Configure()
    {
        Post("custom-validation-failure");
        AllowAnonymous();
        SerializerContext<ManualValidationSerCtx>();
    }

    public override async Task HandleAsync(ManualValidationRequest req, CancellationToken ct)
    {
        if (req.ShouldFail)
        {
            AddError("Intentional failure");
            ThrowIfAnyErrors();
        }

        await Send.OkAsync(new ManualValidationResponse
        {
            Message = "Validation passed",
            ValidationPassed = true
        }, ct);
    }
}

[JsonSerializable(typeof(ManualValidationRequest))]
[JsonSerializable(typeof(ManualValidationResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public partial class ManualValidationSerCtx : JsonSerializerContext;
