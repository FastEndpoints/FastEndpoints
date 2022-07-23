using FluentValidation.Results;

namespace Web.PipelineBehaviors.PreProcessors;

public class SecurityProcessor<TRequest> : IPreProcessor<TRequest>
{
    public Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        var tenantID = ctx.Request.Headers["tenant-id"].FirstOrDefault();

        if (tenantID == null)
        {
            failures.Add(new("MissingHeaders", "The [tenant-id] header needs to be set!"));
            return ctx.Response.SendErrorsAsync(failures);
        }

        return tenantID != "qwerty" ? ctx.Response.SendForbiddenAsync() : Task.CompletedTask;
    }
}