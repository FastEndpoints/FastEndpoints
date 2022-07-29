using FastEndpoints;
using FluentValidation;

namespace FEBench;

public class Request
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
}

public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("name needed");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("last needed");
        RuleFor(x => x.Age).GreaterThan(10).WithMessage("too young");
        RuleFor(x => x.PhoneNumbers).NotEmpty().WithMessage("phone needed");
    }
}

public class Response
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
}

public class Endpoint : Endpoint<Request>
{
    public ILogger<Endpoint>? MyProperty { get; set; }

    //public Endpoint(ILogger<Endpoint> logger)
    //{

    //}

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/benchmark/ok/{id}");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
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