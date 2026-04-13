using System.Net;
using System.Text.Json;

namespace SecurityTests;

public class X402Tests(Sut app) : TestBase<Sut>
{
    [Fact]
    public async Task MissingPaymentHeaderReturns402WithPaymentRequiredHeader()
    {
        using var rsp = await app.GuestClient.GetAsync("/api/test-cases/x402/success");

        rsp.StatusCode.ShouldBe((HttpStatusCode)402);
        rsp.Headers.TryGetValues("PAYMENT-REQUIRED", out var values).ShouldBeTrue();

        var paymentRequired = Decode(values!.Single());
        paymentRequired.GetProperty("x402Version").GetInt32().ShouldBe(2);
        paymentRequired.GetProperty("resource").GetProperty("description").GetString().ShouldBe("Protected success endpoint");
        paymentRequired.GetProperty("accepts")[0].GetProperty("amount").GetString().ShouldBe("1000");
        paymentRequired.GetProperty("accepts")[0].GetProperty("network").GetString().ShouldBe("eip155:84532");
        paymentRequired.GetProperty("accepts")[0].GetProperty("payTo").GetString().ShouldBe("0xdefault");
    }

    [Fact]
    public async Task SuccessfulProtectedRequestSettlesAfterHandlerSuccess()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/test-cases/x402/success");
        req.Headers.Add("PAYMENT-SIGNATURE", PaymentSignature("valid", "1000", "eip155:84532", "0xdefault", "0xasset"));

        using var rsp = await app.GuestClient.SendAsync(req);
        var body = await rsp.Content.ReadAsStringAsync();

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        body.ShouldContain("success");
        rsp.Headers.TryGetValues("PAYMENT-RESPONSE", out var values).ShouldBeTrue();

        var paymentResponse = Decode(values!.Single());
        paymentResponse.GetProperty("success").GetBoolean().ShouldBeTrue();
        paymentResponse.GetProperty("transaction").GetString().ShouldBe("0xtx");
        paymentResponse.GetProperty("requirements").GetProperty("amount").GetString().ShouldBe("1000");
    }

    [Fact]
    public async Task FailedHandlerDoesNotSettleInDefaultMode()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/test-cases/x402/failure");
        req.Headers.Add("PAYMENT-SIGNATURE", PaymentSignature("valid", "1000", "eip155:84532", "0xdefault", "0xasset"));

        using var rsp = await app.GuestClient.SendAsync(req);
        var body = await rsp.Content.ReadAsStringAsync();

        rsp.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        body.ShouldContain("failure");
        rsp.Headers.Contains("PAYMENT-RESPONSE").ShouldBeFalse();
    }

    [Fact]
    public async Task EndpointOverridesAreAppliedToPaymentRequirements()
    {
        using var rsp = await app.GuestClient.GetAsync("/api/test-cases/x402/override");

        rsp.StatusCode.ShouldBe((HttpStatusCode)402);
        var paymentRequired = Decode(rsp.Headers.GetValues("PAYMENT-REQUIRED").Single());
        var accept = paymentRequired.GetProperty("accepts")[0];

        accept.GetProperty("amount").GetString().ShouldBe("2500");
        accept.GetProperty("network").GetString().ShouldBe("eip155:999999");
        accept.GetProperty("payTo").GetString().ShouldBe("0xoverride");
        accept.GetProperty("asset").GetString().ShouldBe("0xoverride-asset");
        paymentRequired.GetProperty("resource").GetProperty("mimeType").GetString().ShouldBe("application/custom+json");
    }

    [Fact]
    public async Task EndpointCanOptIntoSettleBeforeHandler()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/test-cases/x402/pre-settled");
        req.Headers.Add("PAYMENT-SIGNATURE", PaymentSignature("settle-first", "1000", "eip155:84532", "0xdefault", "0xasset"));

        using var rsp = await app.GuestClient.SendAsync(req);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        rsp.Headers.Contains("PAYMENT-RESPONSE").ShouldBeTrue();

        var paymentResponse = Decode(rsp.Headers.GetValues("PAYMENT-RESPONSE").Single());
        paymentResponse.GetProperty("transaction").GetString().ShouldBe("0xtx");
    }

    [Fact]
    public async Task SettlementModeOverrideIsNotLeakedToPaymentRequirements()
    {
        using var rsp = await app.GuestClient.GetAsync("/api/test-cases/x402/pre-settled");

        rsp.StatusCode.ShouldBe((HttpStatusCode)402);

        var paymentRequired = Decode(rsp.Headers.GetValues("PAYMENT-REQUIRED").Single());
        var accept = paymentRequired.GetProperty("accepts")[0];

        accept.TryGetProperty("extra", out var extra).ShouldBeTrue();
        extra.TryGetProperty("settlementMode", out _).ShouldBeFalse();
    }

    static JsonElement Decode(string value)
        => JsonDocument.Parse(Convert.FromBase64String(value)).RootElement.Clone();

    static string PaymentSignature(string token, string amount, string network, string payTo, string asset)
    {
        var payload = new
        {
            x402Version = 2,
            resource = new { url = "https://test.local/resource", description = "test", mimeType = "application/json" },
            accepted = new
            {
                scheme = "exact",
                network,
                amount,
                asset,
                payTo,
                maxTimeoutSeconds = 300
            },
            payload = new { testToken = token }
        };

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload));
    }
}
