using FastEndpoints;
using Microsoft.AspNetCore.Mvc;
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
