using Shouldly;

namespace EventBus;

static class EventBusDeliveryAssertions
{
    public static async Task VerifyAsync<THandler, TModel>(IEnumerable<THandler> handlers,
                                                         Func<THandler, Task> completion,
                                                         Func<THandler, TModel?> capturedModel,
                                                         Func<THandler, CancellationToken> capturedToken,
                                                         TModel model,
                                                         CancellationToken ct) where TModel : class
    {
        foreach (var handler in handlers)
        {
            await completion(handler).WaitAsync(TimeSpan.FromSeconds(5), ct);
            capturedModel(handler).ShouldBeSameAs(model);
            capturedToken(handler).ShouldBe(ct);
        }
    }
}
