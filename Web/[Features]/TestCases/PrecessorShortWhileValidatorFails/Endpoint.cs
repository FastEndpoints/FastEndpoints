using FluentValidation;

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
    public async Task PreProcessAsync(IPreProcessorContext<Request> context, CancellationToken ct)
    {
        await context.HttpContext.Response.SendAsync("hello from pre-processor!");
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