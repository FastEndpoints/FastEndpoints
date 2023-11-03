namespace Web.PipelineBehaviors.PreProcessors;

public class AdminHeaderChecker : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        if (context.Request is Customers.Create.Request &&
            !context.HttpContext.Request.Headers.TryGetValue("tenant-id", out _) &&
            !context.HttpContext.Response.HasStarted)
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);
        }
    }
}