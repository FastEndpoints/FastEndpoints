using FluentValidation;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace TestCases.DataAnnotationCompliant;

public class Request
{
    /// <summary>
    /// id of the plain text request
    /// </summary>
    [Description("CurrentId"), DeniedValues(10)]
    public int Id { get; init; }

    [Required, StringLength(10, MinimumLength = 2)]
    public string Name { get; set; }
}

[Obsolete("Obsolete", false)]
public class Endpoint : Endpoint<Request, Request>
{
    public override void Configure()
    {
        Post("/test-cases/data-annotation");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
        => SendAsync(r);
}