using TestCases.EventHandlingTest;
using TestCases.JobQueueTest;

namespace Messaging;

public class JobQueueTests(Sut App) : TestBase<Sut>
{
    public static readonly TheoryData<DateTime?, DateTime?> JobCreateCases = new()
    {
        { null, null },
        { null, DateTime.UtcNow },
        { DateTime.UtcNow, null },
        { DateTime.UtcNow, DateTime.UtcNow }
    };

    [Theory, MemberData(nameof(JobCreateCases)), Priority(1)]
    public async Task Jobs_Create(DateTime? executeAfter, DateTime? expireOn)
    {
        var cmd = new JobTestCommand();
        var job = cmd.CreateJob<Job>(executeAfter, expireOn);

        job.CommandType.ShouldBe(typeof(JobTestCommand).FullName);

        if (executeAfter.HasValue)
            Assert.Equal(job.ExecuteAfter, executeAfter);
        else
            Assert.Equal(DateTime.UtcNow, job.ExecuteAfter, TimeSpan.FromMilliseconds(100));

        if (expireOn.HasValue)
            Assert.Equal(job.ExpireOn, expireOn);
        else
            Assert.Equal(DateTime.UtcNow.AddHours(4), job.ExpireOn, TimeSpan.FromMilliseconds(100));
    }

    public static readonly TheoryData<DateTime?, DateTime?> JobCreateExceptionCases = new()
    {
        { DateTime.Now, DateTime.Now },
        { DateTime.Now, null },
        { null, DateTime.Now }
    };

    [Theory, MemberData(nameof(JobCreateExceptionCases)), Priority(2)]
    public async Task Jobs_Create_Exception(DateTime? executeAfter, DateTime? expireOn)
    {
        var cmd = new JobTestCommand();
        Assert.Throws<ArgumentException>(() => cmd.CreateJob<Job>(executeAfter, expireOn));
    }

    [Fact, Priority(3), Trait("ExcludeInCiCd", "Yes")]
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

        jobs.ShouldContain(j => j.IsCancelled && j.Counter > 0);
        JobStorage.Jobs.Clear();
    }

    [Fact, Priority(4)]
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
            await Task.Delay(100, Cancellation);

        JobTestCommand.CompletedIDs.Count.ShouldBe(9);
        var expected = new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 };
        JobTestCommand.CompletedIDs.Except(expected).Any().ShouldBeFalse();
        JobStorage.Jobs.Clear();
    }

    [Fact, Priority(5)]
    public async Task Job_Deferred_Execution()
    {
        var cts = new CancellationTokenSource(5000);

        for (var i = 0; i < 10; i++)
        {
            var cmd = new JobTestCommand
            {
                Id = i,
                ShouldThrow = i == 0
            };
            var job = cmd.CreateJob<Job>(executeAfter: i == 1 ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow);
            JobStorage.Jobs.Add(job);
            cmd.TriggerJobExecution();
        }

        while (!cts.IsCancellationRequested && JobTestCommand.CompletedIDs.Count < 9)
            await Task.Delay(100, Cancellation);

        JobTestCommand.CompletedIDs.Count.ShouldBe(9);
        var expected = new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 };
        JobTestCommand.CompletedIDs.Except(expected).Any().ShouldBeFalse();
        JobStorage.Jobs.Clear();
    }

    [Fact, Priority(6)]
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

            result.ShouldBe(guid);

            break;
        }
    }

    [Fact, Priority(7)]
    public async Task Job_Progress_Tracking()
    {
        var name = Guid.NewGuid().ToString();
        var job = new JobProgressTestCommand { Name = name };
        var trackingId = await job.QueueJobAsync(ct: Cancellation);
        var step = 0;
        JobResult<string>? res;

        while (true)
        {
            res = await JobTracker<JobProgressTestCommand>.GetJobResultAsync<JobResult<string>>(trackingId, Cancellation);
            step = res?.CurrentStep ?? 0;

            if (step > 0)
                JobProgressTestCommand.CurrentStep = step;

            if (step == 3)
            {
                await Task.Delay(200);

                break;
            }

            await Task.Delay(100);
        }

        res!.Result.ShouldBe(name);
    }

    [Fact, Priority(8)]
    public async Task Job_Generic_Command()
    {
        var cts = new CancellationTokenSource(5000);
        
        var cmd = new JobTestGenericCommand<SomeEvent>
        {
            Id = 1,
            Event = new SomeEvent()
        };
        await cmd.QueueJobAsync(ct: cts.Token);
        
        while (!cts.IsCancellationRequested && JobTestGenericCommand<SomeEvent>.CompletedIDs.Count == 0)
            await Task.Delay(100, Cancellation);

        JobTestGenericCommand<SomeEvent>.CompletedIDs.Count.ShouldBe(1);
        JobTestGenericCommand<SomeEvent>.CompletedIDs.Contains(1);
        JobStorage.Jobs.Clear();
    }
}