using NativeAotChecker.Endpoints.Jobs;

namespace NativeAotCheckerTests;

public class JobQueueIdempotencyTests(App app) : TestBase<App>
{
    [Fact]
    public async Task Duplicate_Queue_Returns_Same_Tracking_Id_And_Stores_Once()
    {
        var orderId = $"aot-idem-{Guid.NewGuid():N}";

        var (rsp, res, err) = await app.Client.POSTAsync<JobQueueIdempotencyEndpoint, JobQueueIdempotencyRequest, JobQueueIdempotencyResponse>(
                                  new() { OrderId = orderId });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.FirstTrackingId.ShouldNotBe(Guid.Empty);
        res.SecondTrackingId.ShouldBe(res.FirstTrackingId);
        res.StoredCountForKey.ShouldBe(1);
        res.HandlerExecutionCount.ShouldBe(1);
        res.Result.ShouldBe($"{orderId}:a");
    }
}
