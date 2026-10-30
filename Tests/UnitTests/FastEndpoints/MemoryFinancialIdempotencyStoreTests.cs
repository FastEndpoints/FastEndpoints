using FastEndpoints;
using Xunit;

namespace Unit.FastEndpoints;

public class MemoryFinancialIdempotencyStoreTests
{
    static readonly TimeSpan Ttl = TimeSpan.FromSeconds(1);
    static ValueTask<FinancialBeginResult> Begin(MemoryFinancialIdempotencyStore store, string key = "k", byte value = 1)
        => store.TryBeginAsync(key, new byte[] { value }, default);

    [Fact]
    public void Constructor_Validates_Budget_And_Entry_Limits()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryFinancialIdempotencyStore(0))
              .ParamName.ShouldBe("maxStoredBodyBytes");
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryFinancialIdempotencyStore(1, maxEntries: 0))
              .ParamName.ShouldBe("maxEntries");
    }

    [Fact]
    public async Task Active_Never_Expires_And_Concurrent_Begin_Is_Immediate()
    {
        var clock = new Clock();
        var store = new MemoryFinancialIdempotencyStore(time: clock);
        await Begin(store);
        clock.Now += TimeSpan.FromDays(100);
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.InFlight);
        (await Begin(store, value: 2)).Kind.ShouldBe(FinancialBeginKind.Conflict);
    }

    [Fact]
    public async Task Unreplayable_Is_Retained_Until_Retention_Expires()
    {
        var clock = new Clock();
        var store = new MemoryFinancialIdempotencyStore(time: clock);
        var first = await Begin(store);
        (await store.MarkUnreplayableAsync("k", first.ReservationToken!, TimeSpan.FromHours(1), default)).ShouldBe(FinancialSettlementResult.Applied);
        clock.Now += TimeSpan.FromMinutes(59);
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.Unreplayable);
        (await Begin(store, value: 2)).Kind.ShouldBe(FinancialBeginKind.Conflict);
        clock.Now += TimeSpan.FromMinutes(1);
        (await Begin(store, value: 2)).Kind.ShouldBe(FinancialBeginKind.Started);
    }

    [Fact]
    public async Task Expired_Unreplayable_Entries_Reclaim_Global_Capacity()
    {
        var clock = new Clock();
        var store = new MemoryFinancialIdempotencyStore(time: clock, maxEntries: 1);
        var first = await Begin(store);
        await store.MarkUnreplayableAsync("k", first.ReservationToken!, Ttl, default);
        await Should.ThrowAsync<InvalidOperationException>(async () => await Begin(store, "next"));
        clock.Now += Ttl;
        (await Begin(store, "next")).Kind.ShouldBe(FinancialBeginKind.Started);
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(36500L * TimeSpan.TicksPerDay + 1)]
    public async Task Unreplayable_Validates_Retention_Boundaries(long ticks)
    {
        var store = new MemoryFinancialIdempotencyStore();
        var first = await Begin(store);
        (await Should.ThrowAsync<ArgumentOutOfRangeException>(
             () => store.MarkUnreplayableAsync("k", first.ReservationToken!, TimeSpan.FromTicks(ticks), default)))
            .ParamName.ShouldBe("retention");
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.InFlight);
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
        (await store.MarkUnreplayableAsync("k", second.ReservationToken!, Ttl, default)).ShouldBe(FinancialSettlementResult.AlreadySettled);
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
        var first = await store.TryBeginAsync("k", hash, default);
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

    [Fact]
    public async Task Response_Larger_Than_Total_Budget_Is_Rejected_And_Reservation_Stays_Active()
    {
        var store = new MemoryFinancialIdempotencyStore(1);
        var first = await Begin(store);
        await Should.ThrowAsync<InvalidOperationException>(
            () => store.CompleteAsync("k", first.ReservationToken!, new() { Body = [1, 2] }, Ttl, default));
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.InFlight);
        (await store.CompleteAsync("k", first.ReservationToken!, new() { Body = [1] }, Ttl, default))
            .ShouldBe(FinancialSettlementResult.Applied);
    }

    [Fact]
    public async Task Response_Larger_Than_Total_Budget_Reports_Ownership_Before_Capacity()
    {
        var store = new MemoryFinancialIdempotencyStore(1);
        var first = await Begin(store);
        await store.AbandonAsync("k", first.ReservationToken!, default);
        var second = await Begin(store);
        (await store.CompleteAsync("k", first.ReservationToken!, new() { Body = [1, 2] }, Ttl, default))
            .ShouldBe(FinancialSettlementResult.OwnershipLost);
        await store.CompleteAsync("k", second.ReservationToken!, new() { Body = [1] }, Ttl, default);
        (await store.CompleteAsync("k", second.ReservationToken!, new() { Body = [1, 2] }, Ttl, default))
            .ShouldBe(FinancialSettlementResult.AlreadySettled);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_Settlement_With_Earlier_Deadline_Reclaims_At_Each_Deadline(bool unreplayable)
    {
        var clock = new Clock();
        var store = new MemoryFinancialIdempotencyStore(time: clock, maxEntries: 3);
        var later = await Begin(store, "later");
        var earlier = await Begin(store, "earlier");
        await Begin(store, "active");
        await store.CompleteAsync("later", later.ReservationToken!, new(), Ttl * 10, default);
        clock.Now += Ttl;
        if (unreplayable)
            await store.MarkUnreplayableAsync("earlier", earlier.ReservationToken!, Ttl, default);
        else
            await store.CompleteAsync("earlier", earlier.ReservationToken!, new(), Ttl, default);

        clock.Now += Ttl - TimeSpan.FromTicks(1);
        await Should.ThrowAsync<InvalidOperationException>(async () => await Begin(store, "next"));
        clock.Now += TimeSpan.FromTicks(1);
        (await Begin(store, "next")).Kind.ShouldBe(FinancialBeginKind.Started);
        (await Begin(store, "later")).Kind.ShouldBe(FinancialBeginKind.Replay);
        await Should.ThrowAsync<InvalidOperationException>(async () => await Begin(store, "last"));

        clock.Now += Ttl * 8;
        (await Begin(store, "last")).Kind.ShouldBe(FinancialBeginKind.Started);
        (await Begin(store, "active")).Kind.ShouldBe(FinancialBeginKind.InFlight);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Removing_Earliest_Key_And_Reusing_It_Preserves_Later_Deadlines(bool unreplayable)
    {
        var clock = new Clock();
        var store = new MemoryFinancialIdempotencyStore(2, time: clock, maxEntries: 3);
        var first = await Begin(store);
        var later = await Begin(store, "later");
        var active = await Begin(store, "active");
        await store.CompleteAsync("k", first.ReservationToken!, new() { Body = [1] }, Ttl, default);
        await store.CompleteAsync("later", later.ReservationToken!, new() { Body = [2] }, Ttl * 2, default);
        clock.Now += Ttl;
        var replacement = await Begin(store); // Direct removal leaves an earlier, conservative deadline.
        await Should.ThrowAsync<InvalidOperationException>(async () => await Begin(store, "next"));
        (await Begin(store)).Kind.ShouldBe(FinancialBeginKind.InFlight);
        if (unreplayable)
            await store.MarkUnreplayableAsync("k", replacement.ReservationToken!, Ttl * 3, default);
        else
            await store.CompleteAsync("k", replacement.ReservationToken!, new() { Body = [3] }, Ttl * 3, default);

        clock.Now += Ttl;
        (await Begin(store, "next")).Kind.ShouldBe(FinancialBeginKind.Started);
        // Reclamation must also recover the expired response's byte budget.
        (await store.CompleteAsync("active", active.ReservationToken!, new() { Body = [4] }, Ttl * 10, default))
            .ShouldBe(FinancialSettlementResult.Applied);
        (await Begin(store)).Kind.ShouldBe(unreplayable ? FinancialBeginKind.Unreplayable : FinancialBeginKind.Replay);
        await Should.ThrowAsync<InvalidOperationException>(async () => await Begin(store, "last"));
        clock.Now += Ttl * 2;
        (await Begin(store, "last")).Kind.ShouldBe(FinancialBeginKind.Started);
    }

    [Fact]
    public async Task Completion_Before_Next_Expiration_Does_Not_Allocate_Reclamation_Scratch_Space()
    {
        var store = new MemoryFinancialIdempotencyStore(time: new Clock());
        var reservation = await Begin(store);
        var response = new FinancialIdempotencyResponse();
        await store.CompleteAsync("k", reservation.ReservationToken!, response, Ttl, default);
        for (var i = 0; i < 100; i++)
            await store.CompleteAsync("k", reservation.ReservationToken!, response, Ttl, default);

        // These tasks complete synchronously. Allow result tasks, but not scan iterator/snapshot allocations.
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            await store.CompleteAsync("k", reservation.ReservationToken!, response, Ttl, default);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.ShouldBeLessThan(128 * 1000L);
    }

    [Fact]
    public async Task Completion_Reclaims_All_Due_Responses_Before_Checking_Byte_Budget()
    {
        var clock = new Clock();
        var store = new MemoryFinancialIdempotencyStore(2, time: clock);
        var first = await Begin(store, "first");
        var second = await Begin(store, "second");
        var active = await Begin(store, "active");
        await store.CompleteAsync("first", first.ReservationToken!, new() { Body = [1] }, Ttl, default);
        await store.CompleteAsync("second", second.ReservationToken!, new() { Body = [2] }, Ttl, default);
        clock.Now += Ttl - TimeSpan.FromTicks(1);
        await Should.ThrowAsync<InvalidOperationException>(
            () => store.CompleteAsync("active", active.ReservationToken!, new() { Body = [3, 4] }, Ttl, default));
        clock.Now += TimeSpan.FromTicks(1);
        (await store.CompleteAsync("active", active.ReservationToken!, new() { Body = [3, 4] }, Ttl, default))
            .ShouldBe(FinancialSettlementResult.Applied);
        (await Begin(store, "active")).Response!.Body.ShouldBe(new byte[] { 3, 4 });
        (await Begin(store, "first")).Kind.ShouldBe(FinancialBeginKind.Started);
        (await Begin(store, "second")).Kind.ShouldBe(FinancialBeginKind.Started);
    }

    sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
