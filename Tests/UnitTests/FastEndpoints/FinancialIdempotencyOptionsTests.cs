using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Xunit;

namespace Unit.FastEndpoints;

public class FinancialIdempotencyOptionsTests
{
    [Fact]
    public void Defaults()
    {
        var opts = new FinancialIdempotencyOptions();

        opts.HeaderName.ShouldBe("Idempotency-Key");
        opts.Duration.ShouldBe(TimeSpan.FromHours(24));
        opts.ReplayStatusCode.ShouldBeNull();
        opts.AddHeaderToResponse.ShouldBeTrue();
        opts.UseCaseSensitivePaths.ShouldBeFalse();
        opts.MaxResponseBodySize.ShouldBe(128 * 1024 * 1024);
        opts.AdditionalHeaders.ShouldBeEmpty();
        opts.AdditionalHeaders.ShouldNotContain(HeaderNames.UserAgent);
    }

    [Fact]
    public void Scope_Is_Required_And_Default_Duration_Is_Inherited()
    {
        Should.Throw<InvalidOperationException>(() => new FinancialIdempotencyOptions().ApplyDefaults(new()));
        var config = new FinancialIdempotencyConfig { CallerScope = _ => "account", DefaultDuration = TimeSpan.FromMinutes(2) };
        var inherited = new FinancialIdempotencyOptions();
        inherited.ApplyDefaults(config);
        inherited.Duration.ShouldBe(config.DefaultDuration);
        var explicitOptions = new FinancialIdempotencyOptions { Duration = TimeSpan.FromMinutes(3) };
        explicitOptions.ApplyDefaults(config);
        explicitOptions.Duration.ShouldBe(TimeSpan.FromMinutes(3));
    }

    [Fact]
    public void Config_Defaults()
    {
        var cfg = new FinancialIdempotencyConfig();

        cfg.InMemoryStoreSize.ShouldBe(1024L * 1024 * 1024);
        cfg.DefaultDuration.ShouldBe(TimeSpan.FromHours(24));
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, false)]
    [InlineData(1L, true)]
    [InlineData(36500L * TimeSpan.TicksPerDay, true)]
    [InlineData(36500L * TimeSpan.TicksPerDay + 1, false)]
    public void Registration_Validates_Duration_Boundaries(long ticks, bool valid)
    {
        var duration = TimeSpan.FromTicks(ticks);
        var services = new ServiceCollection();

        if (!valid)
        {
            Should.Throw<InvalidOperationException>(() => services.AddFinancialIdempotency(c => c.DefaultDuration = duration))
                  .Message.ShouldBe("Invalid financial idempotency global limits!");
            return;
        }

        services.AddFinancialIdempotency(c => c.DefaultDuration = duration);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<FinancialIdempotencyConfig>().DefaultDuration.ShouldBe(duration);
    }

    [Theory]
    [InlineData(-1L, false)]
    [InlineData(0L, false)]
    [InlineData(1L, true)]
    [InlineData(36500L * TimeSpan.TicksPerDay, true)]
    [InlineData(36500L * TimeSpan.TicksPerDay + 1, false)]
    public void Effective_Options_Validate_Duration_Boundaries(long ticks, bool valid)
    {
        var duration = TimeSpan.FromTicks(ticks);
        foreach (var inherit in new[] { true, false })
        {
            var config = new FinancialIdempotencyConfig
            {
                CallerScope = _ => "account",
                DefaultDuration = inherit ? duration : TimeSpan.FromMinutes(2)
            };
            var opts = new FinancialIdempotencyOptions();
            if (!inherit)
                opts.Duration = duration;

            if (!valid)
            {
                Should.Throw<InvalidOperationException>(() => opts.ApplyDefaults(config))
                      .Message.ShouldBe("Invalid financial idempotency options or missing CallerScope!");
                continue;
            }

            opts.ApplyDefaults(config);
            opts.Duration.ShouldBe(duration);
        }
    }
}
