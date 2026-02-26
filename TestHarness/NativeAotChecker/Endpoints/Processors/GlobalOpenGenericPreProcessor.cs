namespace NativeAotChecker.Endpoints.Processors;

public sealed class OpenGenericGlobalPreProcessor<TRequest> : IPreProcessor<TRequest>
{
    public Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken cancellationToken)
    {
        context.HttpContext.Items["OpenGenericGlobalPreProcessorExecuted"] = true;
        return Task.CompletedTask;
    }
}
