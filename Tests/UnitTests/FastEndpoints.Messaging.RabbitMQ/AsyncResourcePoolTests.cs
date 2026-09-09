using FastEndpoints.Messaging.RabbitMQ;
using Shouldly;
using Xunit;

namespace Unit.Messaging.RabbitMQ;

public class AsyncResourcePoolTests
{
    [Fact]
    public async Task Limits_Concurrent_Leases_And_Reuses_Resources()
    {
        var created = 0;
        await using var pool = new AsyncResourcePool<TestResource>(2, _ => ValueTask.FromResult(new TestResource(++created)));
        await using var first = await pool.RentAsync();
        await using var second = await pool.RentAsync();

        var waiting = pool.RentAsync().AsTask();
        await Task.Delay(50);
        waiting.IsCompleted.ShouldBeFalse();

        await first.DisposeAsync();
        await using var third = await waiting.WaitAsync(TimeSpan.FromSeconds(1));

        created.ShouldBe(2);
        third.Resource.ShouldBeSameAs(first.Resource);
    }

    [Fact]
    public async Task Cancels_While_Waiting_For_A_Lease()
    {
        await using var pool = new AsyncResourcePool<TestResource>(1, _ => ValueTask.FromResult(new TestResource(1)));
        await using var lease = await pool.RentAsync();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(() => pool.RentAsync(cts.Token).AsTask());
    }

    [Fact]
    public async Task Returns_Slot_When_Resource_Creation_Fails()
    {
        var attempts = 0;
        await using var pool = new AsyncResourcePool<TestResource>(
            1,
            _ => ++attempts == 1
                     ? ValueTask.FromException<TestResource>(new InvalidOperationException("Expected failure."))
                     : ValueTask.FromResult(new TestResource(attempts)));

        await Should.ThrowAsync<InvalidOperationException>(() => pool.RentAsync().AsTask());
        await using var lease = await pool.RentAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Returns_Lease_After_An_Operation_Fails()
    {
        var resource = new TestResource(1);
        await using var pool = new AsyncResourcePool<TestResource>(1, _ => ValueTask.FromResult(resource));

        await Should.ThrowAsync<InvalidOperationException>(
            async () =>
            {
                await using var lease = await pool.RentAsync();
                throw new InvalidOperationException("Expected publication failure.");
            });
        await using var next = await pool.RentAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));

        next.Resource.ShouldBeSameAs(resource);
    }

    [Fact]
    public async Task Disposal_Waits_For_Active_Lease_And_Disposes_Its_Resource()
    {
        var resource = new TestResource(1);
        var pool = new AsyncResourcePool<TestResource>(1, _ => ValueTask.FromResult(resource));
        var lease = await pool.RentAsync();

        var disposing = pool.DisposeAsync().AsTask();
        await Task.Delay(50);
        disposing.IsCompleted.ShouldBeFalse();
        await Should.ThrowAsync<ObjectDisposedException>(() => pool.RentAsync().AsTask());

        await lease.DisposeAsync();
        await disposing.WaitAsync(TimeSpan.FromSeconds(1));

        resource.IsDisposed.ShouldBeTrue();
    }

    sealed class TestResource(int id) : IAsyncDisposable
    {
        public int Id { get; } = id;
        public bool IsDisposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
