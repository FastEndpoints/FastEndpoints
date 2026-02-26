namespace TestCases.PreProcessorIsRunOnValidationFailure;

public class MyPreProcessor : IPreProcessor<Request>
{
    public Task PreProcessAsync(IPreProcessorContext<Request> context, CancellationToken ct)
    {
        context.ValidationFailures.Add(new("x", "blah"));
        return Task.CompletedTask;
    }
}
