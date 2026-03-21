using FastEndpoints;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QueueTesting;
using static QueueTesting.QueueTestSupport;

namespace JobQueue;

public partial class JobQueueTests
{
    static JobQueue<RefillTestCommand, FastEndpoints.Void, RefillTestRecord, RefillTestStorage> CreateRefillQueue(RefillTestStorage storage,
                                                                                                                  CancellationTokenSource appStopping)
    {
        Factory.RegisterTestServices(_ => { });
        new RefillTestCommandHandler(storage).RegisterForTesting();

        return new(
            storage,
            new TestHostLifetime(appStopping.Token),
            NullLogger<JobQueue<RefillTestCommand, FastEndpoints.Void, RefillTestRecord, RefillTestStorage>>.Instance);
    }

    static JobQueue<DistributedRefillCommand, FastEndpoints.Void, DistributedRefillRecord, DistributedRefillStorage> CreateDistributedRefillQueue(DistributedRefillStorage storage,
        CancellationTokenSource appStopping)
    {
        Factory.RegisterTestServices(_ => { });
        new DistributedRefillCommandHandler().RegisterForTesting();

        return new(
            storage,
            new TestHostLifetime(appStopping.Token),
            NullLogger<JobQueue<DistributedRefillCommand, FastEndpoints.Void, DistributedRefillRecord, DistributedRefillStorage>>.Instance);
    }

    static JobQueue<ManualCancelTestCommand, FastEndpoints.Void, ManualCancelTestRecord, ManualCancelTestStorage> CreateManualCancelQueue(ManualCancelTestStorage storage,
        CancellationTokenSource appStopping)
    {
        Factory.RegisterTestServices(_ => { });
        new ManualCancelTestCommandHandler().RegisterForTesting();

        return new(
            storage,
            new TestHostLifetime(appStopping.Token),
            NullLogger<JobQueue<ManualCancelTestCommand, FastEndpoints.Void, ManualCancelTestRecord, ManualCancelTestStorage>>.Instance);
    }

    static JobQueue<CancelRaceTestCommand, FastEndpoints.Void, CancelRaceTestRecord, CancelRaceTestStorage> CreateCancelRaceQueue(CancelRaceTestStorage storage,
        CancellationTokenSource appStopping)
    {
        Factory.RegisterTestServices(_ => { });
        new CancelRaceTestCommandHandler().RegisterForTesting();

        return new(
            storage,
            new TestHostLifetime(appStopping.Token),
            NullLogger<JobQueue<CancelRaceTestCommand, FastEndpoints.Void, CancelRaceTestRecord, CancelRaceTestStorage>>.Instance);
    }

    static JobQueue<ResultIgnoringTestCommand, string, ResultIgnoringTestRecord, ResultIgnoringTestStorage> CreateResultIgnoringQueue(ResultIgnoringTestStorage storage,
        CancellationTokenSource appStopping)
    {
        Factory.RegisterTestServices(_ => { });
        new ResultIgnoringTestCommandHandler().RegisterForTesting();

        return new(
            storage,
            new TestHostLifetime(appStopping.Token),
            NullLogger<JobQueue<ResultIgnoringTestCommand, string, ResultIgnoringTestRecord, ResultIgnoringTestStorage>>.Instance);
    }

    static JobQueue<ResultCapableVoidTestCommand, FastEndpoints.Void, ResultCapableVoidTestRecord, ResultCapableVoidTestStorage> CreateResultCapableVoidQueue(
        ResultCapableVoidTestStorage storage,
        CancellationTokenSource appStopping)
    {
        Factory.RegisterTestServices(_ => { });
        new ResultCapableVoidTestCommandHandler().RegisterForTesting();

        return new(
            storage,
            new TestHostLifetime(appStopping.Token),
            NullLogger<JobQueue<ResultCapableVoidTestCommand, FastEndpoints.Void, ResultCapableVoidTestRecord, ResultCapableVoidTestStorage>>.Instance);
    }

    static JobQueue<PersistenceRetryTestCommand, string, PersistenceRetryTestRecord, PersistenceRetryTestStorage> CreatePersistenceRetryQueue(
        PersistenceRetryTestStorage storage,
        CancellationTokenSource appStopping,
        ILogger<JobQueue<PersistenceRetryTestCommand, string, PersistenceRetryTestRecord, PersistenceRetryTestStorage>>? logger = null)
    {
        Factory.RegisterTestServices(_ => { });
        new PersistenceRetryTestCommandHandler(storage).RegisterForTesting();

        return new(
            storage,
            new TestHostLifetime(appStopping.Token),
            logger ?? NullLogger<JobQueue<PersistenceRetryTestCommand, string, PersistenceRetryTestRecord, PersistenceRetryTestStorage>>.Instance);
    }

    static JobQueue<BatchFailureTestCommand, FastEndpoints.Void, BatchFailureTestRecord, BatchFailureTestStorage> CreateBatchFailureQueue(
        BatchFailureTestStorage storage,
        CancellationTokenSource appStopping,
        ILogger<JobQueue<BatchFailureTestCommand, FastEndpoints.Void, BatchFailureTestRecord, BatchFailureTestStorage>>? logger = null)
        => new(
            storage,
            new TestHostLifetime(appStopping.Token),
            logger ?? NullLogger<JobQueue<BatchFailureTestCommand, FastEndpoints.Void, BatchFailureTestRecord, BatchFailureTestStorage>>.Instance);

    static async Task QueueJobsAsync(params ICommand[] commands)
    {
        foreach (var command in commands)
            await command.QueueJobAsync(ct: CancellationToken.None);
    }

    static async Task AssertJobsCompletedAsync(params Task<bool>[] completionTasks)
    {
        var allCompleted = await Task.WhenAll(completionTasks);
        allCompleted.All(static completed => completed).ShouldBeTrue();
    }
}
