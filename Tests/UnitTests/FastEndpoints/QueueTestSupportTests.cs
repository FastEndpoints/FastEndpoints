using Xunit;

namespace QueueTesting;

public class QueueTestSupportTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task completion_helpers_accept_successful_tasks(bool waitForCompletion, bool alreadyCompleted)
    {
        var worker = QueueTestSupport.NewSignal();

        if (alreadyCompleted)
            worker.SetResult();

        var wait = waitForCompletion
                       ? QueueTestSupport.WaitForCompletion(worker.Task)
                       : QueueTestSupport.CompletesWithin(worker.Task, TimeSpan.FromSeconds(3));

        if (!alreadyCompleted)
            worker.SetResult();

        await wait;

        if (!waitForCompletion)
            (await (Task<bool>)wait).ShouldBeTrue();
    }

    [Fact]
    public async Task wait_for_completion_asserts_when_worker_remains_pending()
    {
        var worker = QueueTestSupport.NewSignal();

        await Assert.ThrowsAsync<Shouldly.ShouldAssertException>(() => QueueTestSupport.WaitForCompletion(worker.Task, 0));

        worker.Task.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task completes_within_returns_false_when_worker_remains_pending()
    {
        var worker = QueueTestSupport.NewSignal();

        (await QueueTestSupport.CompletesWithin(worker.Task, TimeSpan.Zero)).ShouldBeFalse();

        worker.Task.IsCompleted.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, false, false)]
    public async Task completion_helpers_propagate_worker_faults(bool waitForCompletion, bool timeoutException, bool alreadyCompleted)
    {
        var worker = QueueTestSupport.NewSignal();
        Exception error = timeoutException ? new TimeoutException("worker failed") : new InvalidOperationException("worker failed");

        if (alreadyCompleted)
            worker.SetException(error);

        var wait = waitForCompletion
                       ? QueueTestSupport.WaitForCompletion(worker.Task)
                       : QueueTestSupport.CompletesWithin(worker.Task, TimeSpan.FromSeconds(3));

        if (!alreadyCompleted)
            worker.SetException(error);

        var observed = await Record.ExceptionAsync(() => wait);

        observed.ShouldBeSameAs(error);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task completion_helpers_propagate_worker_cancellation(bool waitForCompletion, bool alreadyCompleted)
    {
        var worker = QueueTestSupport.NewSignal();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        if (alreadyCompleted)
            worker.SetCanceled(cts.Token);

        var wait = waitForCompletion
                       ? QueueTestSupport.WaitForCompletion(worker.Task)
                       : QueueTestSupport.CompletesWithin(worker.Task, TimeSpan.FromSeconds(3));

        if (!alreadyCompleted)
            worker.SetCanceled(cts.Token);

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);

        error.CancellationToken.ShouldBe(cts.Token);
    }

    [Fact]
    public async Task completes_within_propagates_wait_cancellation()
    {
        var worker = QueueTestSupport.NewSignal();
        using var cts = new CancellationTokenSource();
        var wait = QueueTestSupport.CompletesWithin(worker.Task, TimeSpan.FromSeconds(3), cts.Token);

        cts.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);

        error.CancellationToken.ShouldBe(cts.Token);
        worker.Task.IsCompleted.ShouldBeFalse();
    }
}
