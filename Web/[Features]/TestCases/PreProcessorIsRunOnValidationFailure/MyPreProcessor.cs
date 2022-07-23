using FluentValidation.Results;

namespace TestCases.PreProcessorIsRunOnValidationFailure;

public class MyPreProcessor : IPreProcessor<Request>
{
    public Task PreProcessAsync(Request req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        failures.Add(new("x", "blah"));
        return Task.CompletedTask;
    }
}
