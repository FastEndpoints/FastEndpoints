using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MapControllers
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

    [ApiController]
    [Authorize]
    public class Controllers : ControllerBase
    {
        [AllowAnonymous]
        [HttpPost("/benchmark/ok/{id}")]
        public Task<Response> PostAsync(
            [FromRoute] int id,
            [FromBody] Request req,
            [FromServices] IValidator<Request> validator,
            [FromServices] ILogger<Controllers> _)
        {
            //logger.LogInformation("request received!");

            validator.Validate(req);

            return Task.FromResult<Response>(new()
            {
                Id = id,
                Name = req.FirstName + " " + req.LastName,
                Age = req.Age,
                PhoneNumber = req.PhoneNumbers?.FirstOrDefault()
            });
        }
    }
}
