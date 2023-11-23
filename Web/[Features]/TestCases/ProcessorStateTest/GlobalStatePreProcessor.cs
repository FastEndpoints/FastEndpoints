namespace TestCases.ProcessorStateTest;

public class GlobalStatePreProcessor : GlobalPreProcessor<Thingy>
{
    public GlobalStatePreProcessor(ILogger<GlobalStatePreProcessor> logger)
    {
        logger.LogDebug("PP Injection works...");
    }

    public override Task PreProcessAsync(IPreProcessorContext context, Thingy state, CancellationToken ct)
    {
        state.GlobalStateApplied = true;

        return Task.CompletedTask;
    }
}