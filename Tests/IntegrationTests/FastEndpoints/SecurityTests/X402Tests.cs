using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

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
        paymentResponse.GetProperty("network").GetString().ShouldBe("eip155:84532");
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
        paymentRequired.GetProperty("extensions").GetProperty("bazaar").GetProperty("discoverable").GetBoolean().ShouldBeTrue();
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

        if (accept.TryGetProperty("extra", out var extra))
            extra.TryGetProperty("settlementMode", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task InvalidPaymentPayloadDoesNotSetPaymentResponseHeader()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/test-cases/x402/success");
        req.Headers.Add("PAYMENT-SIGNATURE", PaymentSignature("invalid", "1000", "eip155:84532", "0xdefault", "0xasset"));

        using var rsp = await app.GuestClient.SendAsync(req);

        rsp.StatusCode.ShouldBe((HttpStatusCode)402);
        rsp.Headers.Contains("PAYMENT-RESPONSE").ShouldBeFalse();

        var paymentRequired = Decode(rsp.Headers.GetValues("PAYMENT-REQUIRED").Single());
        paymentRequired.GetProperty("error").GetString().ShouldBe("invalid_test_payment");
    }

    [Fact]
    public async Task PaymentPayloadResourceIsOptional()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/test-cases/x402/success");
        req.Headers.Add("PAYMENT-SIGNATURE", PaymentSignature("valid", "1000", "eip155:84532", "0xdefault", "0xasset", includeResource: false));

        using var rsp = await app.GuestClient.SendAsync(req);

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        rsp.Headers.Contains("PAYMENT-RESPONSE").ShouldBeTrue();
    }

    [Fact]
    public async Task MismatchedAcceptedRequirementIsRejectedBeforeSettlement()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/test-cases/x402/success");
        req.Headers.Add("PAYMENT-SIGNATURE", PaymentSignature("valid", "999", "eip155:84532", "0xdefault", "0xasset"));

        using var rsp = await app.GuestClient.SendAsync(req);

        rsp.StatusCode.ShouldBe((HttpStatusCode)402);
        rsp.Headers.Contains("PAYMENT-RESPONSE").ShouldBeFalse();

        var paymentRequired = Decode(rsp.Headers.GetValues("PAYMENT-REQUIRED").Single());
        paymentRequired.GetProperty("error").GetString().ShouldBe("invalid_payment_requirements");
    }

    [Fact]
    public async Task FutureAuthorizationWindowIsRejectedBeforeSettlement()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/test-cases/x402/success");
        req.Headers.Add(
            "PAYMENT-SIGNATURE",
            PaymentSignature(
                "valid",
                "1000",
                "eip155:84532",
                "0xdefault",
                "0xasset",
                validAfter: DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
                validBefore: DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds()));

        using var rsp = await app.GuestClient.SendAsync(req);

        rsp.StatusCode.ShouldBe((HttpStatusCode)402);

        var paymentRequired = Decode(rsp.Headers.GetValues("PAYMENT-REQUIRED").Single());
        paymentRequired.GetProperty("error").GetString().ShouldBe("invalid_exact_evm_payload_authorization_valid_after");
    }

    [Fact]
    public async Task ExpiredAuthorizationWindowIsRejectedBeforeSettlement()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/api/test-cases/x402/success");
        req.Headers.Add(
            "PAYMENT-SIGNATURE",
            PaymentSignature(
                "valid",
                "1000",
                "eip155:84532",
                "0xdefault",
                "0xasset",
                validAfter: DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(),
                validBefore: DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds()));

        using var rsp = await app.GuestClient.SendAsync(req);

        rsp.StatusCode.ShouldBe((HttpStatusCode)402);

        var paymentRequired = Decode(rsp.Headers.GetValues("PAYMENT-REQUIRED").Single());
        paymentRequired.GetProperty("error").GetString().ShouldBe("invalid_exact_evm_payload_authorization_valid_before");
    }

    static JsonElement Decode(string value)
        => JsonDocument.Parse(Convert.FromBase64String(value)).RootElement.Clone();

    static string PaymentSignature(string token,
                                   string amount,
                                   string network,
                                   string payTo,
                                   string asset,
                                   bool includeResource = true,
                                   long? validAfter = null,
                                   long? validBefore = null)
    {
        validAfter ??= DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds();
        validBefore ??= DateTimeOffset.UtcNow.AddMinutes(4).ToUnixTimeSeconds();

        var payload = new JsonObject
        {
            ["x402Version"] = 2,
            ["accepted"] = new JsonObject
            {
                ["scheme"] = "exact",
                ["network"] = network,
                ["amount"] = amount,
                ["asset"] = asset,
                ["payTo"] = payTo,
                ["maxTimeoutSeconds"] = 300
            },
            ["payload"] = new JsonObject
            {
                ["testToken"] = token,
                ["authorization"] = new JsonObject
                {
                    ["from"] = "0xpayer",
                    ["to"] = payTo,
                    ["value"] = amount,
                    ["validAfter"] = validAfter.Value.ToString(),
                    ["validBefore"] = validBefore.Value.ToString(),
                    ["nonce"] = "0xnonce"
                }
            }
        };

        if (includeResource)
        {
            payload["resource"] = new JsonObject
            {
                ["url"] = "https://test.local/resource",
                ["description"] = "test",
                ["mimeType"] = "application/json"
            };
        }

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload));
    }
}
