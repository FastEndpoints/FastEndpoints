using FastEndpoints;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Unit.FastEndpoints;

public class X402EndpointDefinitionTests
{
    [Fact]
    public void ResponseCacheAfterRequirePaymentThrows()
    {
        var ex = Should.Throw<InvalidOperationException>(() => Factory.Create<X402PaymentThenCacheEndpoint>());

        ex.Message.ShouldBe("x402 payment protected endpoints cannot enable response caching!");
    }

    [Fact]
    public void RequirePaymentAfterResponseCacheThrows()
    {
        var ex = Should.Throw<InvalidOperationException>(() => Factory.Create<X402CacheThenPaymentEndpoint>());

        ex.Message.ShouldBe("x402 payment protected endpoints cannot enable response caching!");
    }

    [Fact]
    public async Task PaidEndpointThrowsWithoutX402Middleware()
    {
        var ep = Factory.Create<X402PaidEndpoint>();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => ep.ExecAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldStartWith("x402 payment middleware did not cover this request.");
        ep.HandlerRan.ShouldBeFalse();
    }

    [Fact]
    public async Task PaidEndpointRunsWhenCoveredByX402Middleware()
    {
        var ep = Factory.Create<X402PaidEndpoint>(ctx => ctx.AddTestServices(s => s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>))));
        ep.HttpContext.Items[X402Middleware.PipelineKey] = true;

        await ep.ExecAsync(TestContext.Current.CancellationToken);

        ep.HandlerRan.ShouldBeTrue();
    }
}

file sealed class X402PaymentThenCacheEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("x402/payment-then-cache");
        RequirePayment("1000", "Protected endpoint");
        ResponseCache(60, ResponseCacheLocation.Any);
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(ct);
}

file sealed class X402CacheThenPaymentEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("x402/cache-then-payment");
        ResponseCache(60, ResponseCacheLocation.Any);
        RequirePayment("1000", "Protected endpoint");
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(ct);
}

file sealed class X402PaidEndpoint : EndpointWithoutRequest
{
    public bool HandlerRan { get; private set; }

    public override void Configure()
    {
        Get("x402/paid");
        RequirePayment("1000", "Protected endpoint");
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        HandlerRan = true;

        return Send.OkAsync(ct);
    }
}
