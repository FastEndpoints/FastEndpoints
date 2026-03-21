using FastEndpoints;
using System.Collections.Concurrent;
using System.Reflection;
using Xunit;
using static QueueTesting.QueueTestSupport;

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

    [Fact]
    public async Task manual_cancellation_marks_state_before_signaling_storage_and_token_callbacks()
    {
        var storage = new ManualCancelTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateManualCancelQueue(storage, appStopping);
        var trackingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await storage.StoreJobAsync(
            new()
            {
                QueueID = JobQueue<ManualCancelTestCommand, FastEndpoints.Void, ManualCancelTestRecord, ManualCancelTestStorage>.QueueID,
                TrackingID = trackingId,
                Command = new ManualCancelTestCommand { TrackingID = trackingId },
                ExecuteAfter = now.AddHours(1),
                ExpireOn = now.AddHours(2)
            },
            CancellationToken.None);

        var field = queue.GetType().GetField("_cancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource?>)field.GetValue(queue)!;
        using var cts = new CancellationTokenSource();
        cancellations[trackingId] = cts;

        var callbackObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cts.Token.Register(() => callbackObserved.TrySetResult(cancellations.TryGetValue(trackingId, out var value) && value is null));

        var method = queue.GetType().GetMethod("CancelJobAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ((Task)method.Invoke(queue, [trackingId, CancellationToken.None])!).WaitAsync(TimeSpan.FromSeconds(5));

        (await callbackObserved.Task.WaitAsync(TimeSpan.FromSeconds(5))).ShouldBeTrue();
        (cancellations.TryGetValue(trackingId, out var state) && state is null).ShouldBeTrue();
        (await storage.WaitForCompletionAsync(trackingId, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        storage.CancelCount.ShouldBe(1);
        storage.FailureCount.ShouldBe(0);

        appStopping.Cancel();
    }

    [Fact]
    public async Task manual_cancellation_retries_storage_when_local_state_is_already_marked()
    {
        var storage = new ManualCancelTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateManualCancelQueue(storage, appStopping);
        var trackingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await storage.StoreJobAsync(
            new()
            {
                QueueID = JobQueue<ManualCancelTestCommand, FastEndpoints.Void, ManualCancelTestRecord, ManualCancelTestStorage>.QueueID,
                TrackingID = trackingId,
                Command = new ManualCancelTestCommand { TrackingID = trackingId },
                ExecuteAfter = now.AddHours(1),
                ExpireOn = now.AddHours(2)
            },
            CancellationToken.None);

        var field = queue.GetType().GetField("_cancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource?>)field.GetValue(queue)!;
        cancellations[trackingId] = null;

        var method = queue.GetType().GetMethod("CancelJobAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await ((Task)method.Invoke(queue, [trackingId, CancellationToken.None])!).WaitAsync(TimeSpan.FromSeconds(5));

        storage.CancelCount.ShouldBe(1);
        (await storage.WaitForCompletionAsync(trackingId, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        appStopping.Cancel();
    }

    [Fact]
    public async Task manual_cancellation_rolls_back_pre_execution_marker_when_storage_cancel_fails()
    {
        var storage = new ManualCancelTestStorage(cancelFailures: 1);
        using var appStopping = new CancellationTokenSource();
        var queue = CreateManualCancelQueue(storage, appStopping);
        var trackingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await storage.StoreJobAsync(
            new()
            {
                QueueID = JobQueue<ManualCancelTestCommand, FastEndpoints.Void, ManualCancelTestRecord, ManualCancelTestStorage>.QueueID,
                TrackingID = trackingId,
                Command = new ManualCancelTestCommand { TrackingID = trackingId },
                ExecuteAfter = now.AddHours(1),
                ExpireOn = now.AddHours(2)
            },
            CancellationToken.None);

        var field = queue.GetType().GetField("_cancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource?>)field.GetValue(queue)!;
        var method = queue.GetType().GetMethod("CancelJobAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        await Should.ThrowAsync<InvalidOperationException>(
            () => ((Task)method.Invoke(queue, [trackingId, CancellationToken.None])!).WaitAsync(TimeSpan.FromSeconds(5)));

        cancellations.ContainsKey(trackingId).ShouldBeFalse();
        storage.CancelCount.ShouldBe(1);
        storage.CancelFailureCount.ShouldBe(1);

        await ((Task)method.Invoke(queue, [trackingId, CancellationToken.None])!).WaitAsync(TimeSpan.FromSeconds(5));

        storage.CancelCount.ShouldBe(2);
        (await storage.WaitForCompletionAsync(trackingId, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        appStopping.Cancel();
    }

    [Fact]
    public async Task manual_cancellation_keeps_marker_while_execution_is_still_in_flight()
    {
        ManualCancelTestCommandHandler.Reset();
        var storage = new ManualCancelTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateManualCancelQueue(storage, appStopping);
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var command = new ManualCancelTestCommand();
        await command.QueueJobAsync(ct: CancellationToken.None);
        await ManualCancelTestCommandHandler.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await JobTracker<ManualCancelTestCommand>.CancelJobAsync(command.TrackingID, CancellationToken.None);
        await ManualCancelTestCommandHandler.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(5));

        var nextCleanupOnField = queue.GetType().GetField("_nextCleanupOn", BindingFlags.Instance | BindingFlags.NonPublic)!;
        nextCleanupOnField.SetValue(queue, DateTime.UtcNow.AddMilliseconds(-1));

        var cancellationsField = queue.GetType().GetField("_cancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource?>)cancellationsField.GetValue(queue)!;

        (await WaitUntilAsync(() => nextCleanupOnField.GetValue(queue) is null, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        var markerPreservedDuringCleanup = await WaitUntilAsync(
                                         () => cancellations.TryGetValue(command.TrackingID, out var state) && state is null,
                                         TimeSpan.FromSeconds(1));

        markerPreservedDuringCleanup.ShouldBeTrue();

        ManualCancelTestCommandHandler.ReleaseAfterCancellation();

        await ManualCancelTestCommandHandler.Finished.WaitAsync(TimeSpan.FromSeconds(5));
        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        await WaitUntilAsync(() => !cancellations.ContainsKey(command.TrackingID), TimeSpan.FromSeconds(5));

        storage.CancelCount.ShouldBe(1);
        storage.FailureCount.ShouldBe(0);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task result_returning_commands_complete_without_result_storage()
    {
        var storage = new ResultIgnoringTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateResultIgnoringQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var command = new ResultIgnoringTestCommand { Payload = "ok" };
        await command.QueueJobAsync(ct: CancellationToken.None);

        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        storage.GetJob(command.TrackingID).ShouldBeOfType<ResultIgnoringTestRecord>();

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task void_commands_do_not_attempt_result_persistence_when_storage_provider_supports_results()
    {
        var storage = new ResultCapableVoidTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateResultCapableVoidQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20));

        var command = new ResultCapableVoidTestCommand();
        await command.QueueJobAsync(ct: CancellationToken.None);

        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        storage.StoreResultCalls.ShouldBe(0);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
