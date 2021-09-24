using ApiExpress;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MvcControllers
{
    public class Request : IRequest
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

    public class Response : IResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
        public string? PhoneNumber { get; set; }
    }

    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> logger;

        public HomeController(ILogger<HomeController> logger)
        {
            this.logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Index(
            [FromRoute] int id,
            [FromBody] Request req)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            //logger.LogInformation("request received!");

            return Ok(new Response()
            {
                Id = id,
                Name = req.FirstName + " " + req.LastName,
                Age = req.Age,
                PhoneNumber = req.PhoneNumbers?.FirstOrDefault()
            });
        }
    }
}