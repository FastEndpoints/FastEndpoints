namespace NativeAotChecker.Endpoints.Errors;

public sealed class UnhandledExceptionEndpoint : EndpointWithoutRequest
{
    public const string Reason = "aot unhandled exception";

    public override void Configure()
    {
        Get("unhandled-exception");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
        => throw new InvalidOperationException(Reason);
}
