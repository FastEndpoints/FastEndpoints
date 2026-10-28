using System.ComponentModel.DataAnnotations;
using ValidationContext = System.ComponentModel.DataAnnotations.ValidationContext;

namespace TestCases.DataAnnotationServices;

public class Request
{
    [ScopedValue]
    public string Value { get; set; } = "root";

    public Child Nested { get; set; } = new() { Value = "nested" };
    public List<Child> Children { get; set; } = [new() { Value = "child" }];
}

public class Child
{
    [ScopedValue]
    public string Value { get; set; }
}

[RegisterService<ValidationState>(LifeTime.Scoped)]
public class ValidationState
{
    public Guid Id { get; } = Guid.NewGuid();
    public List<string> Values { get; } = [];
}

public class ScopedValueAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var state = (ValidationState?)validationContext.GetService(typeof(ValidationState));

        if (state is null)
            return new("Validation service is missing.", [validationContext.MemberName!]);

        state.Values.Add((string)value!);

        return value is "invalid"
                   ? new("Value is invalid.", [validationContext.MemberName!])
                   : ValidationResult.Success;
    }
}

public class Response
{
    public Guid ScopeId { get; set; }
    public List<string> ValidatedValues { get; set; } = [];
}

public class Endpoint(ValidationState state) : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("/test-cases/data-annotation-services");
        Description(b => b.ExcludeFromDescription());
        AllowAnonymous();
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
        => Send.OkAsync(new() { ScopeId = state.Id, ValidatedValues = state.Values }, ct);
}