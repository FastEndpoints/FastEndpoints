using FastEndpoints;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using QueueTesting;
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
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

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
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

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
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

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
        queue.SetLimits(2, TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

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
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new ManualCancelTestCommand();
        await command.QueueJobAsync(ct: CancellationToken.None);
        await ManualCancelTestCommandHandler.Started.WaitAsync(TimeSpan.FromSeconds(5));

        await JobTracker<ManualCancelTestCommand>.CancelJobAsync(command.TrackingID, CancellationToken.None);
        await ManualCancelTestCommandHandler.CancellationObserved.WaitAsync(TimeSpan.FromSeconds(5));

        var cancellationsField = queue.GetType().GetField("_cancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource?>)cancellationsField.GetValue(queue)!;
        var staleCancellationsField = queue.GetType().GetField("_staleCancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var staleCancellations = (ConcurrentQueue<(Guid TrackingId, DateTime ExpireAt)>)staleCancellationsField.GetValue(queue)!;

        staleCancellations.Clear();
        staleCancellations.Enqueue((command.TrackingID, DateTime.UtcNow.AddMilliseconds(-1)));

        (await WaitUntilAsync(() => staleCancellations.IsEmpty, TimeSpan.FromSeconds(5))).ShouldBeTrue();

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
    public async Task manual_cancellation_cleanup_expires_each_marker_individually()
    {
        var storage = new ManualCancelTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateManualCancelQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var executeAfter = DateTime.UtcNow.AddHours(1);
        var first = new ManualCancelTestCommand();
        var second = new ManualCancelTestCommand();

        await first.QueueJobAsync(executeAfter: executeAfter, ct: CancellationToken.None);
        await second.QueueJobAsync(executeAfter: executeAfter, ct: CancellationToken.None);

        await JobTracker<ManualCancelTestCommand>.CancelJobAsync(first.TrackingID, CancellationToken.None);
        await JobTracker<ManualCancelTestCommand>.CancelJobAsync(second.TrackingID, CancellationToken.None);

        var cancellationsField = queue.GetType().GetField("_cancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource?>)cancellationsField.GetValue(queue)!;
        var staleCancellationsField = queue.GetType().GetField("_staleCancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var staleCancellations = (ConcurrentQueue<(Guid TrackingId, DateTime ExpireAt)>)staleCancellationsField.GetValue(queue)!;

        staleCancellations.Clear();
        staleCancellations.Enqueue((first.TrackingID, DateTime.UtcNow.AddMilliseconds(-1)));
        staleCancellations.Enqueue((second.TrackingID, DateTime.UtcNow.AddMinutes(5)));

        (await WaitUntilAsync(
             () => staleCancellations.TryPeek(out var item) && item.TrackingId == second.TrackingID,
             TimeSpan.FromSeconds(5))).ShouldBeTrue();

        cancellations.ContainsKey(first.TrackingID).ShouldBeFalse();
        (cancellations.TryGetValue(second.TrackingID, out var secondState) && secondState is null).ShouldBeTrue();
        storage.CancelCount.ShouldBe(2);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task result_returning_commands_complete_without_result_storage()
    {
        var storage = new ResultIgnoringTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateResultIgnoringQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

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
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new ResultCapableVoidTestCommand();
        await command.QueueJobAsync(ct: CancellationToken.None);

        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        storage.StoreResultCalls.ShouldBe(0);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task successful_execution_retries_result_persistence_even_after_execution_timeout_expires()
    {
        var storage = new PersistenceRetryTestStorage(storeResultFailures: 1);
        using var appStopping = new CancellationTokenSource();
        var queue = CreatePersistenceRetryQueue(storage, appStopping);
        queue.SetLimits(1, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new PersistenceRetryTestCommand
        {
            ExecutionDelay = TimeSpan.FromMilliseconds(50),
            ResultText = "persisted"
        };

        await command.QueueJobAsync(ct: CancellationToken.None);

        (await storage.WaitForResultAsync(command.TrackingID, command.ResultText, TimeSpan.FromSeconds(8))).ShouldBeTrue();
        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(8))).ShouldBeTrue();
        storage.ExecutionCount.ShouldBe(1);
        storage.StoreResultAttempts.ShouldBe(2);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task successful_execution_marks_job_complete_even_after_execution_timeout_expires_during_completion_retry()
    {
        var storage = new PersistenceRetryTestStorage(markCompleteFailures: 1);
        using var appStopping = new CancellationTokenSource();
        var queue = CreatePersistenceRetryQueue(storage, appStopping);
        queue.SetLimits(1, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new PersistenceRetryTestCommand
        {
            ExecutionDelay = TimeSpan.FromMilliseconds(50),
            ResultText = "complete-on-retry"
        };

        await command.QueueJobAsync(ct: CancellationToken.None);

        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(8))).ShouldBeTrue();
        storage.ExecutionCount.ShouldBe(1);
        storage.MarkCompleteAttempts.ShouldBe(2);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task timed_out_execution_retries_failure_persistence_until_it_is_recorded()
    {
        var storage = new PersistenceRetryTestStorage(executionFailurePersistFailures: 1);
        using var appStopping = new CancellationTokenSource();
        var queue = CreatePersistenceRetryQueue(storage, appStopping);
        queue.SetLimits(1, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new PersistenceRetryTestCommand
        {
            WaitForCancellation = true,
            ResultText = "timeout"
        };

        await command.QueueJobAsync(ct: CancellationToken.None);

        (await storage.WaitForFailureRecordedAsync(command.TrackingID, TimeSpan.FromSeconds(8))).ShouldBeTrue();
        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(8))).ShouldBeTrue();
        storage.ExecutionCount.ShouldBe(1);
        storage.ExecutionFailureAttempts.ShouldBe(2);
        storage.RecordedFailureCount.ShouldBe(1);
        storage.WasRecordedFailureCancellation(command.TrackingID).ShouldBeTrue();

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task non_cancellation_failure_retries_failure_persistence_even_after_execution_timeout_window_elapses()
    {
        var storage = new PersistenceRetryTestStorage(executionFailurePersistFailures: 15);
        using var appStopping = new CancellationTokenSource();
        var queue = CreatePersistenceRetryQueue(storage, appStopping);
        queue.SetLimits(1, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new PersistenceRetryTestCommand
        {
            ShouldThrow = true,
            FailureMessage = "boom",
            ResultText = "failed"
        };

        await command.QueueJobAsync(ct: CancellationToken.None);

        (await storage.WaitForFailureRecordedAsync(command.TrackingID, TimeSpan.FromSeconds(8))).ShouldBeTrue();
        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(8))).ShouldBeTrue();
        storage.ExecutionCount.ShouldBe(1);
        storage.ExecutionFailureAttempts.ShouldBeGreaterThan(2);
        storage.RecordedFailureCount.ShouldBe(1);
        storage.WasRecordedFailureCancellation(command.TrackingID).ShouldBeFalse();

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task null_command_deserialization_is_recorded_as_handler_failure()
    {
        var storage = new PersistenceRetryTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreatePersistenceRetryQueue(storage, appStopping);
        var trackingId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await storage.StoreJobAsync(
            new()
            {
                QueueID = JobQueue<PersistenceRetryTestCommand, string, PersistenceRetryTestRecord, PersistenceRetryTestStorage>.QueueID,
                TrackingID = trackingId,
                Command = "corrupted-payload",
                ReturnNullCommandOnGet = true,
                ExecuteAfter = now.AddMinutes(-1),
                ExpireOn = now.AddHours(1)
            },
            CancellationToken.None);

        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        (await storage.WaitForFailureRecordedAsync(trackingId, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        (await storage.WaitForCompletionAsync(trackingId, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        storage.ExecutionCount.ShouldBe(0);
        storage.ExecutionFailureAttempts.ShouldBe(1);
        storage.RecordedFailureCount.ShouldBe(1);
        storage.MarkCompleteAttempts.ShouldBe(0);
        storage.WasRecordedFailureCancellation(trackingId).ShouldBeFalse();

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task handler_failure_still_gets_recorded_when_intermediate_result_retrieval_fails()
    {
        var storage = new PersistenceRetryTestStorage(getResultFailures: 1);
        using var appStopping = new CancellationTokenSource();
        var queue = CreatePersistenceRetryQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var failing = new PersistenceRetryTestCommand
        {
            ShouldThrow = true,
            FailureMessage = "boom",
            ResultText = "failed"
        };
        var succeeding = new PersistenceRetryTestCommand
        {
            ExecutionDelay = TimeSpan.FromMilliseconds(10),
            ResultText = "after-failure"
        };

        await failing.QueueJobAsync(ct: CancellationToken.None);
        await succeeding.QueueJobAsync(ct: CancellationToken.None);

        (await storage.WaitForFailureRecordedAsync(failing.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        (await storage.WaitForCompletionAsync(failing.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        (await storage.WaitForCompletionAsync(succeeding.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        storage.ExecutionCount.ShouldBe(2);
        storage.ExecutionFailureAttempts.ShouldBe(1);
        storage.RecordedFailureCount.ShouldBe(1);
        storage.GetResultAttempts.ShouldBe(1);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task app_shutdown_cancellation_does_not_log_critical_execution_errors()
    {
        var storage = new PersistenceRetryTestStorage();
        using var appStopping = new CancellationTokenSource();
        var logger = new TestLogger<JobQueue<PersistenceRetryTestCommand, string, PersistenceRetryTestRecord, PersistenceRetryTestStorage>>();
        var queue = CreatePersistenceRetryQueue(storage, appStopping, logger);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new PersistenceRetryTestCommand
        {
            WaitForCancellation = true,
            ResultText = "shutdown"
        };

        await command.QueueJobAsync(ct: CancellationToken.None);
        (await WaitUntilAsync(() => storage.ExecutionCount == 1, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        appStopping.Cancel();

        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));

        (await storage.WaitForFailureRecordedAsync(command.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
        storage.WasRecordedFailureCancellation(command.TrackingID).ShouldBeTrue();
        logger.Entries.Any(e => e.Level == LogLevel.Critical).ShouldBeFalse();
    }

    [Fact]
    public async Task shutdown_interrupts_batch_retry_delay()
    {
        var storage = new BatchFailureTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateBatchFailureQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(1));

        (await WaitUntilAsync(() => storage.BatchAttempts == 1, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        appStopping.Cancel();

        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(2));

        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
        storage.BatchAttempts.ShouldBe(1);
    }

    [Fact]
    public async Task shutdown_interrupts_result_persistence_retry_delay()
    {
        var storage = new PersistenceRetryTestStorage(storeResultFailures: 1);
        using var appStopping = new CancellationTokenSource();
        var queue = CreatePersistenceRetryQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMinutes(1));

        var command = new PersistenceRetryTestCommand { ResultText = "retry-delay-interrupted" };

        await command.QueueJobAsync(ct: CancellationToken.None);
        (await WaitUntilAsync(() => storage.StoreResultAttempts == 1, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        await Task.Delay(100);
        appStopping.Cancel();

        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(2));

        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
        storage.StoreResultAttempts.ShouldBe(1);
        storage.MarkCompleteAttempts.ShouldBe(1);
        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();
    }

    [Fact]
    public async Task concurrent_cancel_calls_for_same_untracked_job_do_not_leak_or_throw()
    {
        // When multiple threads call CancelJobAsync for a tracking ID that's not yet in _cancellations,
        // the GetOrAdd factory may run on multiple threads but only one result is stored. The orphaned
        // CancellationTokenSource(0) instances must be disposed to prevent resource leaks.
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

        var method = queue.GetType().GetMethod("CancelJobAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        using var barrier = new Barrier(20);

        var tasks = Enumerable.Range(0, 20).Select(
            _ => Task.Run(
                async () =>
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(5));
                    await ((Task)method.Invoke(queue, [trackingId, CancellationToken.None])!).WaitAsync(TimeSpan.FromSeconds(5));
                })).ToArray();

        await Task.WhenAll(tasks);

        var field = queue.GetType().GetField("_cancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource?>)field.GetValue(queue)!;

        // After cancellation, entry should be null (manual cancellation marker)
        (cancellations.TryGetValue(trackingId, out var state) && state is null).ShouldBeTrue();
        storage.CancelCount.ShouldBeGreaterThanOrEqualTo(1);

        appStopping.Cancel();
    }

    [Fact]
    public async Task concurrent_execute_and_cancel_race_does_not_leak_linked_token_source()
    {
        // When ExecuteCommand and CancelJobAsync race on GetOrAdd for the same tracking ID,
        // one factory result is discarded. The linked CTS created by ExecuteCommand registers
        // a callback on _appCancellation; if not disposed, it leaks permanently.
        // This test verifies that the race is handled correctly by running execute and cancel
        // concurrently for the same job, and checking that _appCancellation remains clean.
        ManualCancelTestCommandHandler.Reset();
        var storage = new ManualCancelTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateManualCancelQueue(storage, appStopping);
        queue.SetLimits(2, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new ManualCancelTestCommand();
        await command.QueueJobAsync(ct: CancellationToken.None);

        // Cancel immediately without waiting for execution to start, maximizing the chance
        // that CancelJobAsync's GetOrAdd races with ExecuteCommand's GetOrAdd.
        await JobTracker<ManualCancelTestCommand>.CancelJobAsync(command.TrackingID, CancellationToken.None);

        // Allow execution to proceed (handler may or may not have started depending on race outcome)
        ManualCancelTestCommandHandler.ReleaseAfterCancellation();

        // Wait for the executor to settle
        var field = queue.GetType().GetField("_cancellations", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cancellations = (ConcurrentDictionary<Guid, CancellationTokenSource?>)field.GetValue(queue)!;
        (await WaitUntilAsync(() => storage.CancelCount >= 1, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        // Verify that appStopping can be cancelled cleanly without triggering leaked callbacks
        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task pre_cancelled_marker_is_not_disposed_when_executor_bails_out_early()
    {
        var storage = new PreCancelledExecutionRaceTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreatePreCancelledExecutionRaceQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new ManualCancelTestCommand();
        await command.QueueJobAsync(ct: CancellationToken.None);
        await storage.BatchFetchStarted.WaitAsync(TimeSpan.FromSeconds(5));

        var cancelTask = JobTracker<ManualCancelTestCommand>.CancelJobAsync(command.TrackingID, CancellationToken.None);
        await storage.CancelStarted.WaitAsync(TimeSpan.FromSeconds(5));

        storage.ReleaseBatchFetch();
        await storage.BatchReturned.WaitAsync(TimeSpan.FromSeconds(5));

        storage.ReleaseCancel();
        await cancelTask.WaitAsync(TimeSpan.FromSeconds(5));
        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        var field = typeof(JobQueue<ManualCancelTestCommand, FastEndpoints.Void, ManualCancelTestRecord, PreCancelledExecutionRaceTestStorage>)
                    .GetField("_preCancelledTokenSource", BindingFlags.Static | BindingFlags.NonPublic)!;
        var sharedMarker = (CancellationTokenSource)field.GetValue(null)!;

        sharedMarker.IsCancellationRequested.ShouldBeTrue();
        await sharedMarker.CancelAsync().ShouldNotThrowAsync();
        storage.CancelCount.ShouldBe(1);

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task manual_cancellation_does_not_allow_reexecution_while_storage_cancel_is_still_in_flight()
    {
        CancelRaceTestCommandHandler.Reset();
        var storage = new CancelRaceTestStorage();
        using var appStopping = new CancellationTokenSource();
        var queue = CreateCancelRaceQueue(storage, appStopping);
        queue.SetLimits(1, Timeout.InfiniteTimeSpan, TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(20));

        var command = new CancelRaceTestCommand();
        await command.QueueJobAsync(ct: CancellationToken.None);
        await CancelRaceTestCommandHandler.Started.WaitAsync(TimeSpan.FromSeconds(5));

        var cancelTask = JobTracker<CancelRaceTestCommand>.CancelJobAsync(command.TrackingID, CancellationToken.None);
        await storage.CancelStarted.WaitAsync(TimeSpan.FromSeconds(5));

        await Task.Delay(200);

        CancelRaceTestCommandHandler.ExecutionCount.ShouldBe(1);
        storage.CancelCount.ShouldBe(1);
        CancelRaceTestCommandHandler.FirstExecutionFinished.IsCompleted.ShouldBeFalse();
        CancelRaceTestCommandHandler.SecondExecutionStarted.IsCompleted.ShouldBeFalse();

        storage.ReleaseCancel();
        await cancelTask.WaitAsync(TimeSpan.FromSeconds(5));
        await CancelRaceTestCommandHandler.FirstExecutionFinished.WaitAsync(TimeSpan.FromSeconds(5));
        (await storage.WaitForCompletionAsync(command.TrackingID, TimeSpan.FromSeconds(5))).ShouldBeTrue();

        await Task.Delay(200);

        CancelRaceTestCommandHandler.ExecutionCount.ShouldBe(1);
        CancelRaceTestCommandHandler.SecondExecutionStarted.IsCompleted.ShouldBeFalse();

        appStopping.Cancel();
        await queue.ExecutorTask.WaitAsync(TimeSpan.FromSeconds(5));
        queue.ExecutorTask.IsCompletedSuccessfully.ShouldBeTrue();
    }
}
