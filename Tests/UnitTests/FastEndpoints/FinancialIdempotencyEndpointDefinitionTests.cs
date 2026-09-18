using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RouteMapper;
using Xunit;

namespace Unit.FastEndpoints;

public class FinancialIdempotencyEndpointDefinitionTests
{
    [Fact]
    public void Financial_After_Fingerprint_Throws()
    {
        var ex = Should.Throw<InvalidOperationException>(() => Factory.Create<FingerprintThenFinancialEndpoint>());
        ex.Message.ShouldContain("FinancialIdempotency()");
        ex.Message.ShouldContain("Idempotency()");
    }

    [Fact]
    public void Fingerprint_After_Financial_Throws()
    {
        var ex = Should.Throw<InvalidOperationException>(() => Factory.Create<FinancialThenFingerprintEndpoint>());
        ex.Message.ShouldContain("Idempotency()");
        ex.Message.ShouldContain("FinancialIdempotency()");
    }
}

[Collection(RouteMapperCollection.Name)]
public class FinancialIdempotencyMapperTests : IDisposable
{
    readonly Func<EndpointDefinition, bool>? _previousFilter;
    readonly string? _previousRoutePrefix;
    readonly Action<EndpointDefinition>? _previousConfigurator;
    readonly bool _previousSerializerConfigured;

    public FinancialIdempotencyMapperTests()
    {
        _previousFilter = Config.EpOpts.Filter;
        _previousRoutePrefix = Config.EpOpts.RoutePrefix;
        _previousConfigurator = Config.EpOpts.Configurator;
        _previousSerializerConfigured = MainExtensions.SerializerConfigured;
        Config.EpOpts.Filter = null;
        Config.EpOpts.RoutePrefix = null;
        Config.EpOpts.Configurator = null;
        MainExtensions.SerializerConfigured = false;
    }

    public void Dispose()
    {
        Config.EpOpts.Filter = _previousFilter;
        Config.EpOpts.RoutePrefix = _previousRoutePrefix;
        Config.EpOpts.Configurator = _previousConfigurator;
        MainExtensions.SerializerConfigured = _previousSerializerConfigured;

        var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(
            provider: testingProvider,
            ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }

    [Fact]
    public async Task Registration_Is_Local_And_Duplicate_Insertion_Throws()
    {
        await using var first = WebApplication.CreateBuilder().Build();
        await using var second = WebApplication.CreateBuilder().Build();
        first.UseFinancialIdempotency();
        Should.Throw<InvalidOperationException>(() => first.UseFinancialIdempotency());
        Should.NotThrow(() => second.UseFinancialIdempotency());
        var branch = ((IApplicationBuilder)first).New();
        Should.Throw<InvalidOperationException>(() => branch.UseFinancialIdempotency());
    }

    [Fact]
    public async Task Mapping_Throws_When_Middleware_Is_Missing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(FinancialOnlyEndpoint)]);
        builder.Services.AddFinancialIdempotency(c => c.CallerScope = _ => "test");
        var app = builder.Build();

        try
        {
            var ex = Should.Throw<InvalidOperationException>(() => app.UseFastEndpoints());
            ex.Message.ShouldBe("Financial idempotency middleware setup is incorrect!");
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Mapping_Throws_When_Store_Is_Missing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(FinancialOnlyEndpoint)]);
        var app = builder.Build();
        app.UseFinancialIdempotency();

        try
        {
            var ex = Should.Throw<InvalidOperationException>(() => app.UseFastEndpoints());
            ex.Message.ShouldBe("Financial idempotency middleware setup is incorrect!");
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    [Fact]
    public async Task Mapping_Succeeds_When_Store_And_Middleware_Are_Registered()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddFastEndpoints([typeof(FinancialOnlyEndpoint)]);
        builder.Services.AddFinancialIdempotency(c => c.CallerScope = _ => "test");
        var app = builder.Build();
        app.UseFinancialIdempotency();

        try
        {
            Should.NotThrow(() => app.UseFastEndpoints());
        }
        finally
        {
            await app.DisposeAsync();
        }
    }
}

file sealed class FingerprintThenFinancialEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("fin/fingerprint-then-financial");
        Idempotency();
        FinancialIdempotency();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class FinancialThenFingerprintEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("fin/financial-then-fingerprint");
        FinancialIdempotency();
        Idempotency();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}

file sealed class FinancialOnlyEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("fin/only");
        AllowAnonymous();
        FinancialIdempotency();
    }

    public override Task HandleAsync(CancellationToken ct)
        => Task.CompletedTask;
}
