using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NativeAotCheckerTests;

public class X402Tests(App app) : TestBase<App>
{
    [Fact]
    public async Task Missing_Payment_Returns_402_With_Payment_Required_Header()
    {
        using var rsp = await app.Client.GetAsync("x402-paid", Cancellation);

        rsp.StatusCode.ShouldBe((HttpStatusCode)402);
        var required = Decode(rsp.Headers.GetValues("PAYMENT-REQUIRED").Single());
        required.GetProperty("x402Version").GetInt32().ShouldBe(2);
        required.GetProperty("resource").GetProperty("description").GetString().ShouldBe("aot paid endpoint");
        var accepts = required.GetProperty("accepts")[0];
        accepts.GetProperty("amount").GetString().ShouldBe("1000");
        accepts.GetProperty("payTo").GetString().ShouldBe("0xdefault");
    }

    [Fact]
    public async Task Valid_Payment_Is_Verified_And_Settled_Through_Facilitator_Client()
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "x402-paid");
        req.Headers.Add("PAYMENT-SIGNATURE", PaymentSignature());

        using var rsp = await app.Client.SendAsync(req, Cancellation);
        var body = await rsp.Content.ReadAsStringAsync(Cancellation);

        rsp.IsSuccessStatusCode.ShouldBeTrue(body);
        body.ShouldContain("paid");
        var settlement = Decode(rsp.Headers.GetValues("PAYMENT-RESPONSE").Single());
        settlement.GetProperty("success").GetBoolean().ShouldBeTrue();
        settlement.GetProperty("transaction").GetString().ShouldBe("0xtx");
        settlement.GetProperty("payer").GetString().ShouldBe("0xpayer");
    }

    static JsonElement Decode(string value)
        => JsonDocument.Parse(Convert.FromBase64String(value)).RootElement.Clone();

    static string PaymentSignature()
    {
        var payload = new JsonObject
        {
            ["x402Version"] = 2,
            ["accepted"] = new JsonObject
            {
                ["scheme"] = "exact",
                ["network"] = "eip155:84532",
                ["amount"] = "1000",
                ["asset"] = "0xasset",
                ["payTo"] = "0xdefault",
                ["maxTimeoutSeconds"] = 300
            },
            ["payload"] = new JsonObject
            {
                ["authorization"] = new JsonObject
                {
                    ["from"] = "0xpayer",
                    ["to"] = "0xdefault",
                    ["value"] = "1000",
                    ["validAfter"] = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds().ToString(),
                    ["validBefore"] = DateTimeOffset.UtcNow.AddMinutes(4).ToUnixTimeSeconds().ToString(),
                    ["nonce"] = "0xnonce"
                }
            }
        };

        return Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(payload));
    }
}
