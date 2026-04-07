namespace NativeAotChecker.Endpoints.Binding;

public class EnumQueryBindingRequest
{
    [QueryParam]
    public SampleCategory Category { get; set; }
}

public class EnumQueryBindingResponse
{
    public SampleCategory Category { get; set; }
}

public enum SampleCategory
{
    One = 1,
    Two = 2
}

public class EnumQueryBindingEndpoint : Endpoint<EnumQueryBindingRequest, EnumQueryBindingResponse>
{
    public override void Configure()
    {
        Get("enum-query-binding");
        AllowAnonymous();
        DontThrowIfValidationFails();
    }

    public override async Task HandleAsync(EnumQueryBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new() { Category = req.Category }, ct);
    }
}
