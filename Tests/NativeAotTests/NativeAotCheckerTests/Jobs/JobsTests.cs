using NativeAotChecker.Endpoints.Jobs;

namespace NativeAotCheckerTests;

public class JobsTests(App app) : TestBase<App>
{
    [Fact]
    public async Task Queue_Jobs_That_Return_Results()
    {
        var ids = new List<(Guid id, Task<TestResult<string>> responseTask)>();

        for (var i = 0; i < 10; i++)
        {
            var id = Guid.NewGuid();
            var task = app.Client.GETAsync<JobQueueEndpoint, JobQueueRequest, string>(new() { Id = id });
            ids.Add((id, task));
        }

        var results = await Task.WhenAll(ids.Select(i => i.responseTask));

        for (var i = 0; i < 10; i++)
        {
            var (id, _) = ids[i];
            var (rsp, res, err) = results[i];

            if (!rsp.IsSuccessStatusCode)
                Assert.Fail(err);

            res.ShouldBe(id.ToString());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Void_Jobs_Use_Public_Result_Type_With_Source_Generated_Storage(bool fail)
    {
        var (rsp, res, err) = await app.Client.GETAsync<VoidJobQueueEndpoint, VoidJobQueueRequest, VoidJobQueueResponse>(new() { Fail = fail });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.IsComplete.ShouldBeTrue();
        res.FailureRecorded.ShouldBe(fail);
        res.StoreAttempts.ShouldBe(fail ? 0 : 1);
        res.GetAttempts.ShouldBe(fail ? 1 : 0);
    }
}