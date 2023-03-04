using FluentValidation.Results;

namespace TestCases.ProcessorStateTest;

public class SecondProcessor : FirstPreProcessor
{
    public override Task PreProcessAsync(Request req, Thingy state, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        state.Name = "jane doe";
        return Task.CompletedTask;
    }
}
