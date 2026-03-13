namespace NativeAotChecker.Endpoints.SerializerCtxGen;

class Data<T>
{
    public T Value { get; set; }
}

sealed class FluentGenericPropRequest
{
    public Data<string> StringData { get; set; }
    public Data<int> IntData { get; set; }
}

sealed class FluentGenericPropResponse
{
    public string Result { get; set; }
}

sealed class FluentGenericPropEndpoint : Ep.Req<FluentGenericPropRequest>.Res<FluentGenericPropResponse>
{
    public override void Configure()
    {
        Post("fluent-generic-prop");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FluentGenericPropRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Result = $"{req.StringData.Value} x{req.IntData.Value}"
            },
            ct);
    }
}