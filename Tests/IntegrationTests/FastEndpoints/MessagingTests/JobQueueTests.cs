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
            await Task.Delay(100, cts.Token);

        var jt = App.Services.GetRequiredService<IJobTracker<JobCancelTestCommand>>();

        foreach (var j in jobs)
            _ = jt.CancelJobAsync(j.TrackingId, cts.Token);

        while (!cts.IsCancellationRequested && !jobs.TrueForAll(j => j.IsCancelled))
            await Task.Delay(100, cts.Token);

        jobs.Should().OnlyContain(j => j.IsCancelled && j.Counter > 0);
        JobStorage.Jobs.Clear();
    }

    [Fact, Priority(2), Trait("ExcludeInCiCd", "Yes")]
    public async Task Job_Cancellation_With_Result()
    {
        var cts = new CancellationTokenSource(5000);
        var jobs = new List<JobCancelWithResultTestCommand>();

        await Parallel.ForAsync(
            0,
            99,
            cts.Token,
            async (_, ct) =>
            {
                var job = new JobCancelWithResultTestCommand();
                jobs.Add(job);
                job.TrackingId = await job.QueueJobAsync(ct: ct);
            });

        while (!cts.IsCancellationRequested && !jobs.TrueForAll(j => j.Counter > 0))
            await Task.Delay(100, cts.Token);

        var jt = App.Services.GetRequiredService<IJobTracker<JobCancelWithResultTestCommand, object>>();

        foreach (var j in jobs)
            _ = jt.CancelJobAsync(j.TrackingId, cts.Token);

        while (!cts.IsCancellationRequested && !jobs.TrueForAll(j => j.IsCancelled))
            await Task.Delay(100, cts.Token);

        jobs.Should().OnlyContain(j => j.IsCancelled && j.Counter > 0);
        JobStorage.Jobs.Clear();
    }

    [Fact, Priority(3)]
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
            await cmd.QueueJobAsync(executeAfter: i == 1 ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow);
        }

        while (!cts.IsCancellationRequested && JobTestCommand.CompletedIDs.Count < 9)
            await Task.Delay(1000);

        JobTestCommand.CompletedIDs.Count.Should().Be(9);
        var expected = new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 };
        JobTestCommand.CompletedIDs.Except(expected).Any().Should().BeFalse();
        JobStorage.Jobs.Clear();
    }

    [Fact, Priority(4)]
    public async Task Jobs_With_Result_Execution()
    {
        var cts = new CancellationTokenSource(5000);
        var dictionaryCommandIdTrackingId = new Dictionary<int, Guid>();

        for (var i = 0; i < 10; i++)
        {
            var cmd = new JobWithResultTestCommand
            {
                Id = i,
                ShouldThrow = i == 0
            };
            var trackingId = await cmd.QueueJobAsync(executeAfter: i == 1 ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow);
            if(i != 1)
                dictionaryCommandIdTrackingId.Add(i, trackingId);
        }

        while (!cts.IsCancellationRequested && JobWithResultTestCommand.CompletedIDs.Count < 9)
            await Task.Delay(1000);

        JobWithResultTestCommand.CompletedIDs.Count.Should().Be(9);
        var expected = dictionaryCommandIdTrackingId.Select(x=>x.Key).ToArray();
        JobWithResultTestCommand.CompletedIDs.Except(expected).Any().Should().BeFalse();
        var jt = App.Services.GetRequiredService<IJobTracker<JobWithResultTestCommand, object>>();
        foreach (var trackingId in dictionaryCommandIdTrackingId.Select(x=>x.Value))
        {
            var result = await jt.GetJobResultAsync(trackingId, cts.Token);
            result.Should().NotBeNull();
        }
        JobStorage.Jobs.Clear();
    }
}