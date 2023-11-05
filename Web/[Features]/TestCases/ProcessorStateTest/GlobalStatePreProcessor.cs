using FluentValidation.Results;

namespace TestCases.ProcessorStateTest;

public class GlobalStatePreProcessor : GlobalPreProcessor<Thingy>
{
    public GlobalStatePreProcessor(ILogger<GlobalStatePreProcessor> logger)
    {
        logger.LogInformation("PP Injection works, and you should only see me once");
    }
    
    public override Task PreProcessAsync(IPreProcessorContext context, Thingy state, CancellationToken ct)
    {
        state.GlobalStateApplied = true;

        return Task.CompletedTask;
    }
}