using FluentValidation.Results;

namespace Web.PipelineBehaviors.PostProcessors;

public class MyResponseLogger<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
{
    public Task PostProcessAsync(TRequest req, TResponse res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct)
    {
        var logger = ctx.Resolve<ILogger<TResponse>>();

        if (res is Sales.Orders.Create.Response response)
        {
            logger.LogWarning($"sale complete: {response?.OrderID}");
        }

        return Task.CompletedTask;
    }
}