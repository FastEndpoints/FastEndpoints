namespace TestCases.ProcessorStateTest;

public class SecondProcessor : FirstPreProcessor
{
    public override Task PreProcessAsync(IPreProcessorContext<Request> context, Thingy state, CancellationToken ct)
    {
        state.Name = "jane doe";
        return Task.CompletedTask;
    }
}
