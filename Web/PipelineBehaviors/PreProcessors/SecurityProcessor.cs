namespace Web.PipelineBehaviors.PreProcessors;

public class SecurityProcessor<TRequest> : IPreProcessor<TRequest>
    where TRequest : notnull
{
    public Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        var httpContext = context.HttpContext;

        var tenantID = httpContext.Request.Headers["tenant-id"].FirstOrDefault();

        if (tenantID == null)
        {
            context.ValidationFailures.Add(new("MissingHeaders", "The [tenant-id] header needs to be set!"));

            return httpContext.Response.SendErrorsAsync(context.ValidationFailures);
        }

        return tenantID != "qwerty" ? httpContext.Response.SendForbiddenAsync() : Task.CompletedTask;
    }
}