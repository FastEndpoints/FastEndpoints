using FastEndpoints;
using RichardSzalay.MockHttp;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Unit.FastEndpoints;

public class X402FacilitatorClientTests
{
    [Fact]
    public async Task VerifyAsyncSendsSpecRequestBody()
    {
        MockHttpMessageHandler mockHttp = new();
        mockHttp.Expect(HttpMethod.Post, "https://facilitator.test/verify")
                .Respond(
                    async req =>
                    {
                        var body = await req.Content!.ReadAsStringAsync();
                        var json = JsonDocument.Parse(body).RootElement;

                        json.GetProperty("x402Version").GetInt32().ShouldBe(X402Constants.Version);
                        json.GetProperty("paymentPayload").GetProperty("x402Version").GetInt32().ShouldBe(X402Constants.Version);
                        json.GetProperty("paymentPayload").TryGetProperty("resource", out _).ShouldBeFalse();
                        json.GetProperty("paymentRequirements").GetProperty("amount").GetString().ShouldBe("1000");

                        return new(System.Net.HttpStatusCode.OK)
                        {
                            Content = JsonContent.Create(new VerificationResponse { IsValid = true, Payer = "0xpayer" }, options: X402Serializer.Options)
                        };
                    });

        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("https://facilitator.test/");

        IX402FacilitatorClient client = new X402FacilitatorClient(http);

        var response = await client.VerifyAsync(CreateVerificationRequest(), CancellationToken.None);

        response.IsValid.ShouldBeTrue();
        response.Payer.ShouldBe("0xpayer");
    }

    [Fact]
    public async Task VerifyAsyncWorksWithoutTrailingSlashInBaseAddress()
    {
        MockHttpMessageHandler mockHttp = new();
        mockHttp.Expect(HttpMethod.Post, "https://facilitator.test/platform/v2/x402/verify")
                .Respond(JsonContent.Create(new VerificationResponse { IsValid = true }, options: X402Serializer.Options));

        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("https://facilitator.test/platform/v2/x402");

        IX402FacilitatorClient client = new X402FacilitatorClient(http);

        var response = await client.VerifyAsync(CreateVerificationRequest(), CancellationToken.None);

        response.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyAsyncNonJsonErrorPreservesHttpFailureDetails()
    {
        MockHttpMessageHandler mockHttp = new();
        mockHttp.Expect(HttpMethod.Post, "https://facilitator.test/verify")
                .Respond(
                    req => new(System.Net.HttpStatusCode.BadGateway)
                    {
                        Content = new StringContent("<html>bad gateway</html>")
                    });

        var http = mockHttp.ToHttpClient();
        http.BaseAddress = new("https://facilitator.test/");

        IX402FacilitatorClient client = new X402FacilitatorClient(http);

        var ex = await Should.ThrowAsync<InvalidOperationException>(
                     () => client.VerifyAsync(
                         new()
                         {
                             PaymentPayload = new()
                             {
                                 X402Version = X402Constants.Version,
                                 Accepted = new()
                                 {
                                     Scheme = "exact",
                                     Network = "eip155:84532",
                                     Amount = "1000",
                                     Asset = "0xasset",
                                     PayTo = "0xpayto",
                                     MaxTimeoutSeconds = 300
                                 }
                             },
                             PaymentRequirements = new()
                             {
                                 Scheme = "exact",
                                 Network = "eip155:84532",
                                 Amount = "1000",
                                 Asset = "0xasset",
                                 PayTo = "0xpayto",
                                 MaxTimeoutSeconds = 300
                             }
                         },
                         CancellationToken.None));

        ex.Message.ShouldBe("facilitator call [verify] failed with status [502]: <html>bad gateway</html>");
    }

    static VerificationRequest CreateVerificationRequest()
        => new()
        {
            PaymentPayload = new()
            {
                X402Version = X402Constants.Version,
                Accepted = new()
                {
                    Scheme = "exact",
                    Network = "eip155:84532",
                    Amount = "1000",
                    Asset = "0xasset",
                    PayTo = "0xpayto",
                    MaxTimeoutSeconds = 300
                },
                Payload = new()
                {
                    ["signature"] = "0xsig"
                }
            },
            PaymentRequirements = new()
            {
                Scheme = "exact",
                Network = "eip155:84532",
                Amount = "1000",
                Asset = "0xasset",
                PayTo = "0xpayto",
                MaxTimeoutSeconds = 300
            }
        };
}