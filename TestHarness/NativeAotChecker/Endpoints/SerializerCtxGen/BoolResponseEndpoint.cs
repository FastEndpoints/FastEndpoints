namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class BoolResponseEndpoint : EndpointWithoutRequest<bool>
{
    public override void Configure()
    {
        Get("ser-ctx-gen-bool-response");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(true);
    }
}