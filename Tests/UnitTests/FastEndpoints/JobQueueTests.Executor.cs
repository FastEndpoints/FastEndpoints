using FastEndpoints;
using Xunit;

namespace JobQueue;

public partial class JobQueueTests
{
    [Fact]
    public async Task executor_refills_a_freed_slot_without_waiting_for_the_whole_batch()
    {
        RefillTestCommandHandler.Reset();
        var storage = new RefillTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateRefillQueue(storage, appStopping);
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var slow = new RefillTestCommand { Name = "slow", Sequence = 0 };
        var fast = new RefillTestCommand { Name = "fast", Sequence = 1 };
        var third = new RefillTestCommand { Name = "third", Sequence = 2 };

        await QueueJobsAsync(slow, fast, third);
        await RefillTestCommandHandler.FastStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var thirdStartedBeforeSlowFinished = await Task.WhenAny(
                                                 RefillTestCommandHandler.ThirdStarted,
                                                 Task.Delay(TimeSpan.FromSeconds(1))) ==
                                             RefillTestCommandHandler.ThirdStarted;

        thirdStartedBeforeSlowFinished.ShouldBeTrue();
        storage.MaxActiveExecutions.ShouldBe(2);
        storage.GetRequestedLimitsSnapshot().ShouldContain(2);

        RefillTestCommandHandler.ReleaseSlow();

        await AssertJobsCompletedAsync(
            storage.WaitForCompletionAsync(slow.TrackingID, TimeSpan.FromSeconds(5)),
            storage.WaitForCompletionAsync(fast.TrackingID, TimeSpan.FromSeconds(5)),
            storage.WaitForCompletionAsync(third.TrackingID, TimeSpan.FromSeconds(5)));

        appStopping.Cancel();
    }

    [Fact]
    public async Task executor_waits_for_in_flight_jobs_to_finish_during_shutdown()
    {
        RefillTestCommandHandler.Reset();
        var storage = new RefillTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateRefillQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var drain = new RefillTestCommand { Name = "drain", Sequence = 0 };
        await QueueJobsAsync(drain);
        await RefillTestCommandHandler.DrainStarted.WaitAsync(TimeSpan.FromSeconds(5));

        appStopping.Cancel();
        await Task.Delay(200);

        queue.ExecutorTask.IsCompleted.ShouldBeFalse();

        RefillTestCommandHandler.ReleaseDrain();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));

        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
        (await storage.WaitForCompletionAsync(drain.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
    }

    [Fact]
    public async Task executor_drains_in_flight_jobs_when_shutdown_occurs_during_semaphore_wait()
    {
        RefillTestCommandHandler.Reset();
        var storage = new RefillTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateRefillQueue(storage, appStopping);
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var drain = new RefillTestCommand { Name = "drain", Sequence = 0 };
        await QueueJobsAsync(drain);
        await RefillTestCommandHandler.DrainStarted.WaitAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(200);
        appStopping.Cancel();
        await Task.Delay(200);

        queue.ExecutorTask.IsCompleted.ShouldBeFalse();

        RefillTestCommandHandler.ReleaseDrain();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));

        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
        (await storage.WaitForCompletionAsync(drain.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
    }

    [Fact]
    public async Task distributed_executor_only_claims_available_slots_when_refilling()
    {
        DistributedRefillCommandHandler.Reset();
        var storage = new DistributedRefillStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateDistributedRefillQueue(storage, appStopping);
        queue.SetLimits(2, TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(20));

        var slow = new DistributedRefillCommand { Name = "slow", Sequence = 0 };
        var fast = new DistributedRefillCommand { Name = "fast", Sequence = 1 };
        var third = new DistributedRefillCommand { Name = "third", Sequence = 2 };
        var fourth = new DistributedRefillCommand { Name = "fourth", Sequence = 3 };

        await QueueJobsAsync(slow, fast, third, fourth);
        await DistributedRefillCommandHandler.FastStarted.WaitAsync(TimeSpan.FromSeconds(5));
        await DistributedRefillCommandHandler.ThirdStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var fourthStartedEarly = await Task.WhenAny(
                                     DistributedRefillCommandHandler.FourthStarted,
                                     Task.Delay(TimeSpan.FromMilliseconds(300))) ==
                                 DistributedRefillCommandHandler.FourthStarted;

        fourthStartedEarly.ShouldBeFalse();
        storage.GetRequestedLimitsSnapshot().ShouldContain(1);
        storage.GetDequeueAfter(fourth.TrackingID).ShouldBe(DateTime.MinValue);

        DistributedRefillCommandHandler.ReleaseThird();
        await DistributedRefillCommandHandler.FourthStarted.WaitAsync(TimeSpan.FromSeconds(5));

        storage.GetDequeueAfter(fourth.TrackingID).ShouldBeGreaterThan(DateTime.UtcNow.AddSeconds(-5));

        DistributedRefillCommandHandler.ReleaseSlow();

        await AssertJobsCompletedAsync(
            storage.WaitForCompletionAsync(slow.TrackingID, TimeSpan.FromSeconds(5)),
            storage.WaitForCompletionAsync(fast.TrackingID, TimeSpan.FromSeconds(5)),
            storage.WaitForCompletionAsync(third.TrackingID, TimeSpan.FromSeconds(5)),
            storage.WaitForCompletionAsync(fourth.TrackingID, TimeSpan.FromSeconds(5)));

        appStopping.Cancel();
    }
}
