namespace NativeAotChecker.Endpoints.Binding;

public readonly struct StructRequest
{
    public int Id { get; init; }
    public string Name { get; init; }
    public double Value { get; init; }
}

public readonly struct StructResponse
{
    public int Id { get; init; }
    public string Name { get; init; }
    public double Value { get; init; }
    public bool IsValid { get; init; }
}

public sealed class StructTypesEndpoint : Endpoint<StructRequest, StructResponse>
{
    public override void Configure()
    {
        Post("struct-types-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(StructRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Id = req.Id,
                Name = req.Name,
                Value = req.Value,
                IsValid = req.Id > 0 && !string.IsNullOrEmpty(req.Name)
            },
            ct);
    }
}