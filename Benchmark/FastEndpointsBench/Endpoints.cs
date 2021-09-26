using FastEndpoints;
using FluentValidation;

namespace FastEndpointsBench
{
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

    public class Endpoint : Endpoint<Request, Validator>
    {
        public ILogger<Endpoint>? MyProperty { get; set; }

        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/benchmark/ok/{id}");
            AllowAnnonymous();
        }

        protected override Task ExecuteAsync(Request req, CancellationToken ct)
        {
            //Logger.LogInformation("request received!");

            return SendAsync(new Response()
            {
                Id = req.Id,
                Name = req.FirstName + " " + req.LastName,
                Age = req.Age,
                PhoneNumber = req.PhoneNumbers?.FirstOrDefault()
            });
        }
    }
}
