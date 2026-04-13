using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Xunit;

namespace Unit.FastEndpoints;

public class X402RegistrationExtensionsTests
{
    [Fact]
    public async Task AddX402AllowsCustomizingFacilitatorHttpClientBuilder()
    {
        var cfg = new Config();
        RecordingHandler.SeenAuthorization = null;
        var originalUrl = cfg.X402.FacilitatorUrl;
        cfg.X402.FacilitatorUrl = "https://facilitator.test";

        try
        {
            var services = new ServiceCollection();

            services.AddTransient<TestAuthHandler>();
            services.AddX402(
                builder =>
                {
                    builder.ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler());
                    builder.AddHttpMessageHandler<TestAuthHandler>();
                });

            await using var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<IX402FacilitatorClient>();

            var response = await client.VerifyAsync(
                               new()
                               {
                                   PaymentPayload = new()
                                   {
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
                               },
                               CancellationToken.None);

            response.IsValid.ShouldBeTrue();
            RecordingHandler.SeenAuthorization.ShouldBe("Bearer test-token");
        }
        finally
        {
            cfg.X402.FacilitatorUrl = originalUrl;
        }
    }

    sealed class TestAuthHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = new("Bearer", "test-token");

            return base.SendAsync(request, cancellationToken);
        }
    }

    sealed class RecordingHandler : HttpMessageHandler
    {
        internal static string? SeenAuthorization { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SeenAuthorization = request.Headers.Authorization?.ToString();

            return Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new VerificationResponse { IsValid = true }, options: X402Serializer.Options)
                });
        }
    }
}
