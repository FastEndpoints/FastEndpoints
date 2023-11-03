namespace Web.PipelineBehaviors.PreProcessors;

public class MyRequestLogger<TRequest> : IPreProcessor<TRequest> 
    where TRequest : notnull
{
    public Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        var logger = context.HttpContext.Resolve<ILogger<TRequest>>();

        logger.LogInformation($"request:{context.Request?.GetType().FullName} path: {context.HttpContext.Request.Path}");

        return Task.CompletedTask;
    }
}
