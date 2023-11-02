using FluentValidation.Results;

namespace TestCases.ProcessorStateTest;

public class GlobalStatePreProcessor : GlobalPreProcessor<Thingy>
{
    public override Task PreProcessAsync(object req, Thingy state, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct)
    {
        state.GlobalStateApplied = true;
        return Task.CompletedTask;
    }
}