using FluentValidation.Results;

namespace TestCases.ProcessorStateTest;

public class RequestDurationLogger : PostProcessor<Request, Thingy, string>
{
    public override Task PostProcessAsync(Request req, Thingy state, string res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct)
    {
        var logger = ctx.Resolve<ILogger<RequestDurationLogger>>();
        logger.LogInformation("Requst took: {@duration} ms.", state.Duration);
        return Task.CompletedTask;
    }
}
