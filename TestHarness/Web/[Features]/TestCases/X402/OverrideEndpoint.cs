namespace TestCases.X402;

sealed class OverrideEndpoint : EndpointWithoutRequest<Response>
{
    public override void Configure()
    {
        Get("/test-cases/x402/override");
        AllowAnonymous();
        RequirePayment(
            "2500",
            "Protected override endpoint",
            o =>
            {
                o.Network = "eip155:999999";
                o.PayTo = "0xoverride";
                o.Asset = "0xoverride-asset";
                o.MimeType = "application/custom+json";
                o.Extensions = new()
                {
                    ["bazaar"] = new JsonObject
                    {
                        ["discoverable"] = true
                    }
                };
            });
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(new() { Message = "override" }, ct);
}