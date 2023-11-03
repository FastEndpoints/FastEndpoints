namespace TestCases.ProcessorStateTest;

public class RequestDurationLogger : PostProcessor<Request, Thingy, string>
{
    public override Task PostProcessAsync(IPostProcessorContext<Request, string> context, Thingy state, CancellationToken ct)
    {
        var logger = context.HttpContext.Resolve<ILogger<RequestDurationLogger>>();
        logger.LogInformation("Requst took: {@duration} ms.", state.Duration);

        return Task.CompletedTask;
    }
}