using FastEndpoints;
using RichardSzalay.MockHttp;
using Xunit;

namespace Unit.FastEndpoints;

public class X402FacilitatorClientTests
{
    [Fact]
    public async Task VerifyAsyncNonJsonErrorPreservesHttpFailureDetails()
    {
        MockHttpMessageHandler mockHttp = new();
        mockHttp.Expect(HttpMethod.Post, "https://facilitator.test/verify")
                .Respond(req => new(System.Net.HttpStatusCode.BadGateway)
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
                                 Resource = new() { Url = "https://test.local/resource", Description = "test" },
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
}
