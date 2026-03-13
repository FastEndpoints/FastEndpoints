namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class NestedPage<T>
{
    public List<T> Items { get; set; }
}

sealed class NestedEnvelope<T>
{
    public NestedPage<T> Page { get; set; }
}

sealed class NestedGenericPropRequest
{
    public NestedEnvelope<string> StringData { get; set; }
}

sealed class NestedGenericPropResponse
{
    public string Result { get; set; }
}

sealed class NestedGenericPropEndpoint : Endpoint<NestedGenericPropRequest, NestedGenericPropResponse>
{
    public override void Configure()
    {
        Post("nested-generic-prop");
        AllowAnonymous();
    }

    public override async Task HandleAsync(NestedGenericPropRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Result = string.Join(",", req.StringData.Page.Items)
            },
            ct);
    }
}
