namespace Web.PipelineBehaviors.PreProcessors;

public class AdminHeaderChecker : IGlobalPreProcessor
{
    public Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        if (context.Request is Customers.Create.Request &&
            !context.HttpContext.Request.Headers.TryGetValue("tenant-id", out _) &&
            !context.HttpContext.Response.HasStarted)
            return context.HttpContext.Response.SendForbiddenAsync(ct);

        return Task.CompletedTask;
    }
}