using FastEndpoints;
using FluentValidation;

namespace FEBench;

public class ThrottleRequest
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
}

public class ThrottleValidator : Validator<ThrottleRequest>
{
    public ThrottleValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("name needed");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("last needed");
        RuleFor(x => x.Age).GreaterThan(10).WithMessage("too young");
        RuleFor(x => x.PhoneNumbers).NotEmpty().WithMessage("phone needed");
    }
}

public class ThrottleResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
}

public class ThrottleEndpoint : Endpoint<ThrottleRequest>
{
    public ILogger<Endpoint>? MyProperty { get; set; }

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/benchmark/throttle/{id}");
        AllowAnonymous();
        Throttle(5000000, 300);
    }

    public override Task HandleAsync(ThrottleRequest req, CancellationToken ct)
    {
        //Logger.LogInformation("request received!");

        //validator is automatically being run by FastEndpoints

        return SendAsync(new Response()
        {
            Id = req.Id,
            Name = req.FirstName + " " + req.LastName,
            Age = req.Age,
            PhoneNumber = req.PhoneNumbers?.FirstOrDefault()
        });
    }
}