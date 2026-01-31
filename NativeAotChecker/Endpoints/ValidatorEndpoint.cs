using System.Text.Json.Serialization;
using FluentValidation;

namespace NativeAotChecker.Endpoints;

// Test: FluentValidation in AOT mode
public sealed class ValidatorRequest
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Email { get; set; } = string.Empty;
}

public sealed class ValidatorResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class ValidatorRequestValidator : Validator<ValidatorRequest>
{
    public ValidatorRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(50);

        RuleFor(x => x.Age)
            .InclusiveBetween(1, 150);

        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}

public sealed class ValidatorEndpoint : Endpoint<ValidatorRequest, ValidatorResponse>
{
    public override void Configure()
    {
        Post("validator-test");
        AllowAnonymous();
        SerializerContext<ValidatorSerCtx>();
    }

    public override async Task HandleAsync(ValidatorRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new ValidatorResponse
        {
            IsValid = true,
            Message = $"Valid request for {req.Name}, age {req.Age}"
        }, ct);
    }
}

// Test: Validation failure response in AOT
public sealed class ValidatorFailureEndpoint : Endpoint<ValidatorRequest, ValidatorResponse>
{
    public override void Configure()
    {
        Post("validator-failure-test");
        AllowAnonymous();
        SerializerContext<ValidatorSerCtx>();
    }

    public override async Task HandleAsync(ValidatorRequest req, CancellationToken ct)
    {
        // This should not be reached if validation fails
        await Send.OkAsync(new ValidatorResponse
        {
            IsValid = true,
            Message = "Should not reach here"
        }, ct);
    }
}

[JsonSerializable(typeof(ValidatorRequest))]
[JsonSerializable(typeof(ValidatorResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public partial class ValidatorSerCtx : JsonSerializerContext;
