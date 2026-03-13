namespace NativeAotChecker.Endpoints.SerializerCtxGen;

sealed class GenericRequest<T>
{
    public T? Value { get; set; }
}

sealed class GenericRequestResponse
{
    public string? Result { get; set; }
}

sealed class GenericRequestEndpoint : Endpoint<GenericRequest<string>, GenericRequestResponse>
{
    public override void Configure()
    {
        Post("generic-request");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GenericRequest<string> req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Result = req.Value
            },
            ct);
    }
}
