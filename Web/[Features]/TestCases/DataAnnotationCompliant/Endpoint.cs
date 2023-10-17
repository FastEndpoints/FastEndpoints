using FluentValidation;
using System.ComponentModel.DataAnnotations;

namespace TestCases.DataAnnotationCompliant;


public class Request
{
    /// <summary>
    /// id of the plain text request
    /// </summary>
    public int Id { get; set; }

    [Required, StringLength(10, MinimumLength = 2)]
    public string Name { get; set; }


    public class RequestValidator : Validator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Id).InclusiveBetween(10, 99);

            RuleFor(x => x.Name)
                .Must((model, x) => x.Contains("v", StringComparison.OrdinalIgnoreCase))
                .WithMessage("the Name required a char:v")
                ;
        }
    }
}



public class Endpoint : Endpoint<Request, Request>
{
    public override void Configure()
    {
        Post("/test-cases/data-annotation");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        return SendAsync(r);
    }
}