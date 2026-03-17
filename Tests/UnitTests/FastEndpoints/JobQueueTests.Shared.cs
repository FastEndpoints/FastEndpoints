using FastEndpoints;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobQueue;

public partial class JobQueueTests
{
    static TaskCompletionSource<bool> NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);

        while (!cts.IsCancellationRequested)
        {
            if (condition())
                return true;

            try
            {
                await Task.Delay(25, cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return condition();
    }

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

    sealed class TestHostLifetime(CancellationToken appStoppingToken) : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => appStoppingToken;
        public CancellationToken ApplicationStopped => CancellationToken.None;

        public void StopApplication() { }
    }
}