using FastEndpoints;
using FluentValidation;

namespace FEBench;

public readonly struct StructRequest
{
    public int Id { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public int Age { get; init; }
    public IEnumerable<string>? PhoneNumbers { get; init; }
}

public class StructValidator : Validator<StructRequest>
{
    public StructValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("name needed");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("last needed");
        RuleFor(x => x.Age).GreaterThan(10).WithMessage("too young");
        RuleFor(x => x.PhoneNumbers).NotEmpty().WithMessage("phone needed");
    }
}

public class StructResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
}

public class StructEndpoint : Endpoint<StructRequest>
{
    public ILogger<StructEndpoint>? MyProperty { get; set; }

    //public Endpoint(ILogger<Endpoint> logger)
    //{

    //}

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/benchmark/struct/{id}");
        AllowAnonymous();
    }

    public override Task HandleAsync(StructRequest req, CancellationToken ct)

        //Logger.LogInformation("request received!");
        //validator is automatically being run by FastEndpoints
        => SendAsync(
            new StructResponse
            {
                Id = req.Id,
                Name = req.FirstName + " " + req.LastName,
                Age = req.Age,
                PhoneNumber = req.PhoneNumbers?.FirstOrDefault()
            });
}