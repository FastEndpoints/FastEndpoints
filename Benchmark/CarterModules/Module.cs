using Carter;
using Carter.ModelBinding;
using Carter.Request;
using Carter.Response;
using FluentValidation;

namespace CarterModules;

public class Request
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
}

public class Validator : AbstractValidator<Request>
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

public class Module : CarterModule
{
    public Module(ILogger<Module> logger)
    {
        Post("/benchmark/ok/{id}", async (req, res) =>
        {
            //logger.LogInformation("request received!");

            var (ValidationResult, Data) = await req.BindAndValidate<Request>();

            await res.AsJson(new Response()
            {
                Id = req.RouteValues.As<int>("id"),
                Name = Data.FirstName + " " + Data.LastName,
                Age = Data.Age,
                PhoneNumber = Data.PhoneNumbers?.FirstOrDefault()
            });
        });
    }
}