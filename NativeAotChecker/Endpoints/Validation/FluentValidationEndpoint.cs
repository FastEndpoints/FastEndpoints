using FluentValidation;

namespace NativeAotChecker.Endpoints.Validation;

public class FluentValidationRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class FluentValidationResponse
{
    public string Message { get; set; } = string.Empty;
}

public class FluentValidationValidator : Validator<FluentValidationRequest>
{
    public FluentValidationValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required!")
            .EmailAddress()
            .WithMessage("Invalid email format!");

        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("Full name is required!")
            .MinimumLength(3)
            .WithMessage("Full name must be at least 3 characters!");

        RuleFor(x => x.Age)
            .GreaterThan(0)
            .WithMessage("Age must be greater than 0!")
            .LessThanOrEqualTo(120)
            .WithMessage("Age must be realistic (max 120)!");
    }
}

public sealed class FluentValidationEndpoint : Endpoint<FluentValidationRequest, FluentValidationResponse>
{
    public override void Configure()
    {
        Post("fluent-validation");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FluentValidationRequest r, CancellationToken c)
    {
        await Send.OkAsync(new() { Message = $"Hello {r.FullName}! Your email {r.Email} has been validated." }, c);
    }
}