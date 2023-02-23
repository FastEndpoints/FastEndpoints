using FastEndpoints;
using FluentValidation;

namespace FEBench;

public class ScopedValidatorRequest
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
}

public class ScopedValidator : Validator<ScopedValidatorRequest>
{
    public ScopedValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("name needed");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("last needed");
        RuleFor(x => x.Age).GreaterThan(10).WithMessage("too young");
        RuleFor(x => x.PhoneNumbers).NotEmpty().WithMessage("phone needed");
    }
}

public class ScopedValidatorResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
}

public class ScopedValidatorEndpoint : Endpoint<ScopedValidatorRequest>
{
    public ILogger<ScopedValidatorEndpoint>? MyProperty { get; set; }
    public ScopedValidator? Validator { get; set; }

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/benchmark/scoped-validator/{id}");
        AllowAnonymous();
    }

    public async override Task HandleAsync(ScopedValidatorRequest req, CancellationToken ct)
    {
        //Logger.LogInformation("request received!");

        await Validator!.ValidateAsync(req, ct);

        await SendAsync(new ScopedValidatorResponse()
        {
            Id = req.Id,
            Name = req.FirstName + " " + req.LastName,
            Age = req.Age,
            PhoneNumber = req.PhoneNumbers?.FirstOrDefault()
        });
    }
}