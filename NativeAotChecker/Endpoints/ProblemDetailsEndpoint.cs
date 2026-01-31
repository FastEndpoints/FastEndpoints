using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: ProblemDetails response in AOT mode
public sealed class ErrorDetailsRequest
{
    [QueryParam]
    public int ErrorCode { get; set; } = 400;

    [QueryParam]
    public string ErrorMessage { get; set; } = "Something went wrong";

    [QueryParam]
    public bool ShouldSucceed { get; set; } = false;
}

public sealed class ErrorDetailsSuccessResponse
{
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public sealed class ErrorDetailsEndpoint : Endpoint<ErrorDetailsRequest, ErrorDetailsSuccessResponse>
{
    public override void Configure()
    {
        Get("error-details-test");
        AllowAnonymous();
        SerializerContext<ErrorDetailsSerCtx>();
    }

    public override async Task HandleAsync(ErrorDetailsRequest req, CancellationToken ct)
    {
        if (req.ShouldSucceed)
        {
            await Send.OkAsync(new ErrorDetailsSuccessResponse
            {
                Message = "Operation completed successfully",
                Success = true
            }, ct);
            return;
        }

        // Test ThrowError which should produce ProblemDetails-like response
        ThrowError(req.ErrorMessage);
    }
}

// Test: ValidationProblemDetails response
public sealed class ValidationErrorRequest
{
    public string RequiredField { get; set; } = string.Empty;
    public int MustBePositive { get; set; }
}

public sealed class ValidationErrorEndpoint : Endpoint<ValidationErrorRequest, ErrorDetailsSuccessResponse>
{
    public override void Configure()
    {
        Post("validation-error-test");
        AllowAnonymous();
        SerializerContext<ErrorDetailsSerCtx>();
    }

    public override async Task HandleAsync(ValidationErrorRequest req, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(req.RequiredField))
            AddError(r => r.RequiredField, "RequiredField is required");

        if (req.MustBePositive <= 0)
            AddError(r => r.MustBePositive, "MustBePositive must be greater than 0");

        ThrowIfAnyErrors();

        await Send.OkAsync(new ErrorDetailsSuccessResponse
        {
            Message = $"Validated: {req.RequiredField}, {req.MustBePositive}",
            Success = true
        }, ct);
    }
}

[JsonSerializable(typeof(ErrorDetailsRequest))]
[JsonSerializable(typeof(ErrorDetailsSuccessResponse))]
[JsonSerializable(typeof(ValidationErrorRequest))]
public partial class ErrorDetailsSerCtx : JsonSerializerContext;
