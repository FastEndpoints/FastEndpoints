namespace NativeAotChecker.Endpoints.SerializerCtxGen;

class Wrapper<T>
{
    public T Value { get; set; }
}

sealed class GenericPropRequest
{
    public Wrapper<string> StringData { get; set; }
    public Wrapper<int> IntData { get; set; }
}

sealed class GenericPropResponse
{
    public string Result { get; set; }
}

sealed class GenericPropEndpoint : Endpoint<GenericPropRequest, GenericPropResponse>
{
    public override void Configure()
    {
        Post("generic-prop");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GenericPropRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Result = $"{req.StringData.Value} x{req.IntData.Value}"
            },
            ct);
    }
}