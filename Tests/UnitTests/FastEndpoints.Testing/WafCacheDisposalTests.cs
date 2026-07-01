using FastEndpoints.Testing;
using Xunit;

namespace Testing;

public class WafCacheDisposalTests
{
    [Fact]
    public async Task dispose_cache_disposes_waf_and_runs_hook_once()
    {
        var waf = new DisposableWaf();
        var hookCalls = 0;

        TestFixture.CacheWaf(
            typeof(CachedFixture),
            waf,
            () =>
            {
                hookCalls++;

                return ValueTask.CompletedTask;
            });

        await BaseFixture.DisposeWafCacheAsync(typeof(CachedFixture).Assembly);
        await BaseFixture.DisposeWafCacheAsync(typeof(CachedFixture).Assembly);

        waf.DisposeCalls.ShouldBe(1);
        hookCalls.ShouldBe(1);
    }

    sealed class CachedFixture : TestFixture
    {
    }

    abstract class TestFixture : BaseFixture
    {
        public static void CacheWaf(Type fixtureType, object waf, Func<ValueTask> onWafDisposed)
        {
            WafCache[fixtureType] = new AsyncLazy<object>(() => Task.FromResult(waf));
            RegisterWafDisposedHook(fixtureType, onWafDisposed);
        }
    }

    sealed class DisposableWaf : IAsyncDisposable
    {
        public int DisposeCalls { get; private set; }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;

            return ValueTask.CompletedTask;
        }
    }
}
