using TestCases.JobQueueTest;

namespace Messaging;

public class JobQueueTests(Sut App) : TestBase<Sut>
{
    [Fact, Priority(1), Trait("ExcludeInCiCd", "Yes")]
    public async Task Job_Cancellation()
    {
        var cts = new CancellationTokenSource(5000);
        var jobs = new List<JobCancelTestCommand>();

        await Parallel.ForAsync(
            0,
            99,
            cts.Token,
            async (_, ct) =>
            {
                var job = new JobCancelTestCommand();
                jobs.Add(job);
                job.TrackingId = await job.QueueJobAsync(ct: ct);
            });

        while (!cts.IsCancellationRequested && !jobs.TrueForAll(j => j.Counter > 0))
            await Task.Delay(250, cts.Token);

        var jobTracker = App.Services.GetRequiredService<IJobTracker<JobCancelTestCommand>>();

        foreach (var j in jobs)
            _ = jobTracker.CancelJobAsync(j.TrackingId, cts.Token);

        while (!cts.IsCancellationRequested && !jobs.TrueForAll(j => j.IsCancelled))
            await Task.Delay(100, cts.Token);

        jobs.Should().OnlyContain(j => j.IsCancelled && j.Counter > 0);
        JobStorage.Jobs.Clear();
    }

    [Fact, Priority(2)]
    public async Task Jobs_Execution()
    {
        var cts = new CancellationTokenSource(5000);

        for (var i = 0; i < 10; i++)
        {
            var cmd = new JobTestCommand
            {
                Id = i,
                ShouldThrow = i == 0
            };
            await cmd.QueueJobAsync(executeAfter: i == 1 ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow, ct: cts.Token);
        }

        while (!cts.IsCancellationRequested && JobTestCommand.CompletedIDs.Count < 9)
            await Task.Delay(100);

        JobTestCommand.CompletedIDs.Count.Should().Be(9);
        var expected = new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 };
        JobTestCommand.CompletedIDs.Except(expected).Any().Should().BeFalse();
        JobStorage.Jobs.Clear();
    }

    [Fact, Priority(3)]
    public async Task Job_With_Result_Execution()
    {
        var cts = new CancellationTokenSource(5000);
        var guid = Guid.NewGuid();
        var job = new JobWithResultTestCommand { Id = guid };
        var trackingId = await job.QueueJobAsync(ct: cts.Token);
        var jobTracker = App.Services.GetRequiredService<IJobTracker<JobWithResultTestCommand>>();

        while (!cts.IsCancellationRequested)
        {
            var result = await jobTracker.GetJobResultAsync<Guid>(trackingId, cts.Token);

            if (result == default)
            {
                await Task.Delay(100, cts.Token);

                continue;
            }

            result.Should().Be(guid);

            break;
        }
    }
}