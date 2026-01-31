using FastEndpoints;
using FluentValidation;

namespace NativeAotChecker.Endpoints;

// Request with validation rules
public class FluentValidatorRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int Age { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
}

public class FluentValidatorResponse
{
    public bool IsValid { get; set; }
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
}

/// <summary>
/// FluentValidation validator with expression-based rules.
/// AOT ISSUE: RuleFor(x => x.Property) uses expression trees.
/// Expression compilation happens at runtime.
/// PropertyName extraction uses reflection.
/// </summary>
public class FluentValidatorRequestValidator : Validator<FluentValidatorRequest>
{
    public FluentValidatorRequestValidator()
    {
        // Expression tree: x => x.Email
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        // Expression tree: x => x.Password
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain uppercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a number");

        // Expression tree: x => x.Age
        RuleFor(x => x.Age)
            .GreaterThanOrEqualTo(18).WithMessage("Must be 18 or older")
            .LessThanOrEqualTo(120).WithMessage("Invalid age");

        // Expression tree: x => x.PhoneNumber
        RuleFor(x => x.PhoneNumber)
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Invalid phone number format")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber));
    }
}

/// <summary>
/// Tests FluentValidation integration in AOT mode.
/// AOT ISSUE: Validator discovery uses assembly scanning and reflection.
/// RuleFor uses expression trees which need runtime compilation.
/// Property name resolution uses reflection.
/// </summary>
public class FluentValidatorEndpoint : Endpoint<FluentValidatorRequest, FluentValidatorResponse>
{
    public override void Configure()
    {
        Post("fluent-validator-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FluentValidatorRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new FluentValidatorResponse
        {
            IsValid = true,
            Email = req.Email,
            Age = req.Age
        });
    }
}
