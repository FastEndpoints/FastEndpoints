using FluentValidation.Results;

namespace Web.PipelineBehaviors.PreProcessors;

public class MyRequestLogger<TRequest> : IPreProcessor<TRequest>
{
    public Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        var logger = ctx.Resolve<ILogger<TRequest>>();

        logger.LogInformation($"request:{req?.GetType().FullName} path: {ctx.Request.Path}");

        return Task.CompletedTask;
    }
}
