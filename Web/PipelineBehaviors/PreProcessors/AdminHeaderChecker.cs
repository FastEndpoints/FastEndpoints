using FluentValidation.Results;

namespace Web.PipelineBehaviors.PreProcessors;

public class AdminHeaderChecker : IGlobalPreProcessor
{
    public async Task PreProcessAsync(object req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        if (req is Customers.Create.Request && !ctx.Request.Headers.TryGetValue("tenant-id", out _) && !ctx.Response.HasStarted)
        {
            await ctx.Response.SendForbiddenAsync(ct);
        }
    }
}