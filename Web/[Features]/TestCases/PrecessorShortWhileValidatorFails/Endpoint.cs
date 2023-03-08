using FluentValidation;
using FluentValidation.Results;

namespace TestCases.PrecessorShortWhileValidatorFails;

public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(10);
    }
}

public class Request
{
    public int Id { get; set; }
}

public class Processor : IPreProcessor<Request>
{
    public async Task PreProcessAsync(Request req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        await ctx.Response.SendAsync("hello from pre-processor!");
    }
}

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Get("testcases/pre-processor-shortcircuit-while-validator-fails");
        AllowAnonymous();
        PreProcessors(new Processor());
    }

    public override async Task HandleAsync(Request r, CancellationToken c)
    {
        await SendAsync("ok!");
    }
}