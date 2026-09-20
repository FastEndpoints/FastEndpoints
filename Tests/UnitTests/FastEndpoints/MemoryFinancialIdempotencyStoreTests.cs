using FastEndpoints;
using Xunit;

namespace Unit.FastEndpoints;

public class MemoryFinancialIdempotencyStoreTests
{
    static readonly TimeSpan Ttl = TimeSpan.FromSeconds(1);
    static ValueTask<FinancialBeginResult> Begin(MemoryFinancialIdempotencyStore store, string key = "k", byte value = 1)
        => store.TryBeginAsync(key, new byte[] { value }, Ttl, default);

    [Fact]
    public void Constructor_Validates_Budget_And_Entry_Limits()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryFinancialIdempotencyStore(0))
              .ParamName.ShouldBe("maxStoredBodyBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryFinancialIdempotencyStore(1, maxEntries: 0))
              .ParamName.ShouldBe("maxEntries");
    }

    [Fact]
    public async Task Active_And_Uncertain_Never_Expire_And_Concurrent_Begin_Is_Immediate()
    {
        var clock = new Clock();
        var store = new MemoryFinancialIdempotencyStore(time: clock);
        var first = await Begin(store);
        clock.Now += TimeSpan.FromDays(100);
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.InFlight);
        (await Begin(store, value: 2)).Kind.ShouldBe(FinancialBeginKind.Conflict);
        await store.MarkUnreplayableAsync("k", first.ReservationToken!, default);
        clock.Now += TimeSpan.FromDays(100);
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.Unreplayable);
    }

    [Fact]
    public async Task Ownership_Is_Fenced_And_Terminal_Records_Are_Immutable()
    {
        var store = new MemoryFinancialIdempotencyStore();
        var first = await Begin(store);
        await store.AbandonAsync("k", first.ReservationToken!, default);
        var second = await Begin(store);
        (await store.CompleteAsync("k", first.ReservationToken!, new(), Ttl, default)).ShouldBe(FinancialSettlementResult.OwnershipLost);
        (await store.AbandonAsync("k", first.ReservationToken!, default)).ShouldBe(FinancialSettlementResult.OwnershipLost);
        (await store.CompleteAsync("k", second.ReservationToken!, new() { StatusCode = 201 }, Ttl, default)).ShouldBe(FinancialSettlementResult.Applied);
        (await store.AbandonAsync("k", second.ReservationToken!, default)).ShouldBe(FinancialSettlementResult.AlreadySettled);
        (await store.MarkUnreplayableAsync("k", second.ReservationToken!, default)).ShouldBe(FinancialSettlementResult.AlreadySettled);
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.Replay);
    }

    [Fact]
    public async Task Expired_Completed_Entries_Reclaim_Global_Capacity()
    {
        var clock = new Clock();
        var store = new MemoryFinancialIdempotencyStore(1, time: clock, maxEntries: 1);
        var first = await Begin(store);
        await store.CompleteAsync("k", first.ReservationToken!, new() { Body = [1] }, Ttl, default);
        await Should.ThrowAsync<InvalidOperationException>(async () => await Begin(store, "next"));
        clock.Now += TimeSpan.FromSeconds(2);
        var next = await Begin(store, "next");
        (await store.CompleteAsync("next", next.ReservationToken!, new() { Body = [2] }, Ttl, default)).ShouldBe(FinancialSettlementResult.Applied);
    }

    [Fact]
    public async Task Storage_And_Replay_Are_Defensive_Snapshots()
    {
        var store = new MemoryFinancialIdempotencyStore();
        byte[] hash = [1];
        var first = await store.TryBeginAsync("k", hash, Ttl, default);
        hash[0] = 2;
        byte[] body = [3];
        string[] values = ["original"];
        await store.CompleteAsync("k", first.ReservationToken!, new() { Body = body, Headers = [new("X", values)] }, Ttl, default);
        body[0] = 4;
        values[0] = "changed";
        var replay = (await Begin(store)).Response!;
        replay.Body[0].ShouldBe((byte)3);
        replay.Headers[0].Value[0].ShouldBe("original");
        replay.Body[0] = 9;
        (await Begin(store)).Response!.Body[0].ShouldBe((byte)3);
    }

    [Fact]
    public async Task Concurrent_Completions_Cannot_Exceed_Budget()
    {
        var store = new MemoryFinancialIdempotencyStore(1);
        var a = await Begin(store, "a");
        var b = await Begin(store, "b");
        var applied = 0;
        await Task.WhenAll(new[] { ("a", a), ("b", b) }.Select(pair => Task.Run(async () =>
        {
            try
            {
                await store.CompleteAsync(pair.Item1, pair.Item2.ReservationToken!, new() { Body = [1] }, Ttl, default);
                Interlocked.Increment(ref applied);
            }
            catch (InvalidOperationException) { }
        })));
        applied.ShouldBe(1);
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, false)]
    [InlineData(1L, true)]
    [InlineData(36500L * TimeSpan.TicksPerDay, true)]
    [InlineData(36500L * TimeSpan.TicksPerDay + 1, false)]
    public async Task Completion_Validates_Duration_Boundaries(long ticks, bool valid)
    {
        var ttl = TimeSpan.FromTicks(ticks);
        var clock = new Clock { Now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var store = new MemoryFinancialIdempotencyStore(time: clock);
        var first = await Begin(store);

        if (!valid)
        {
            var exception = await Should.ThrowAsync<ArgumentOutOfRangeException>(
                () => store.CompleteAsync("k", first.ReservationToken!, new(), ttl, default));
            exception.ParamName.ShouldBe("ttl");
            (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.InFlight);

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var cancelled = await Should.ThrowAsync<OperationCanceledException>(
                () => store.CompleteAsync("k", first.ReservationToken!, new(), ttl, cancellation.Token));
            cancelled.CancellationToken.ShouldBe(cancellation.Token);
            return;
        }

        (await store.CompleteAsync("k", first.ReservationToken!, new() { StatusCode = 201 }, ttl, default))
            .ShouldBe(FinancialSettlementResult.Applied);
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.Replay);
        clock.Now += ttl;
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.Started);
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
