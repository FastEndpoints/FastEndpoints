namespace Web.PipelineBehaviors.PostProcessors;

public class MyResponseLogger<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
{
    public Task PostProcessAsync(PostProcessorContext<TRequest, TResponse> context, CancellationToken ct)
    {
        var logger = context.HttpContext.Resolve<ILogger<TResponse>>();

        if (context.Response is Sales.Orders.Create.Response response)
        {
            logger.LogWarning($"sale complete: {response?.OrderID}");
        }

        return Task.CompletedTask;
    }
}