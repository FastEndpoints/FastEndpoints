using FastEndpoints;
using FastEndpoints.Validation.Results;

namespace Web.PipelineBehaviors.PostProcessors
{
    public class MyResponseLogger<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
    {
        public Task PostProcessAsync(TRequest req, TResponse? res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct)
        {
            var logger = ctx.RequestServices.GetRequiredService<ILogger<TResponse>>();

            if (res?.GetType() == typeof(Sales.Orders.Create.Response))
            {
                var response = res as Sales.Orders.Create.Response;

                logger.LogWarning($"sale complete: {response?.OrderID}");
            }

            return Task.CompletedTask;
        }
    }
}
