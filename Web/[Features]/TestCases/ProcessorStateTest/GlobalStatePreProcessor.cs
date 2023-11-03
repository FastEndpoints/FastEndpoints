using FluentValidation.Results;

namespace TestCases.ProcessorStateTest;

public class GlobalStatePreProcessor : GlobalPreProcessor<Thingy>
{
    public override Task PreProcessAsync(IPreProcessorContext context, Thingy state, CancellationToken ct)
    {
        state.GlobalStateApplied = true;

        return Task.CompletedTask;
    }
}