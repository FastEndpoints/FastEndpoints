using System.Text;

namespace NativeAotChecker.Endpoints.X402;

// stands in for the remote facilitator so the real X402FacilitatorClient serialization path runs without network access
sealed class FakeFacilitatorHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var json = request.RequestUri!.AbsolutePath.EndsWith("/verify", StringComparison.Ordinal)
                       ? """{"isValid":true,"payer":"0xpayer"}"""
                       : """{"success":true,"transaction":"0xtx","network":"eip155:84532","payer":"0xpayer"}""";

        return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }
}
