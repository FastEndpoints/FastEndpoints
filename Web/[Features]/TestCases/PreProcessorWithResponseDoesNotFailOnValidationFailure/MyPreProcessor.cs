using FluentValidation.Results;

namespace TestCases.PreProcessorWithResponseDoesNotFailOnValidationFailure;

public class MyPreProcessor : IPreProcessor<PreProcessorWithResponseDoesNotFailOnValidationFailure.Request>
{
    public Task PreProcessAsync(PreProcessorWithResponseDoesNotFailOnValidationFailure.Request req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        return ctx.Response.SendErrorsAsync(failures);
    }
}
