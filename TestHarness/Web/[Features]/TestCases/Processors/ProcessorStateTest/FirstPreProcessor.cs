namespace TestCases.ProcessorStateTest;

public class FirstPreProcessor : PreProcessor<Request, Thingy>
{
    public override Task PreProcessAsync(IPreProcessorContext<Request> context, Thingy state, CancellationToken ct)
    {
        state.Id = context.Request!.Id;
        state.Name = "john doe";

        return Task.CompletedTask;
    }
}